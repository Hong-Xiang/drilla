import { createRaymarchingUniforms } from "../raymarching-uniforms";

const clearColor: GPUColor = { r: 0.03, g: 0.21, b: 0.26, a: 1 };
const fullScreenVertices = new Float32Array([
  -1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1,
]);
const fullScreenVertexLayout: GPUVertexBufferLayout = {
  arrayStride: 8,
  stepMode: "vertex",
  attributes: [{ shaderLocation: 0, offset: 0, format: "float32x2" }],
};

type ShaderName =
  | "MinimumTriangleShader"
  | "SimpleStructUniformShaderModule"
  | "MandelbrotDistanceShaderModule"
  | "RaymarchingPrimitiveShader";

interface UniformBinding {
  readonly binding: number;
  readonly data: AllowSharedBufferSource;
}

interface VertexInput {
  readonly layout: GPUVertexBufferLayout;
  readonly data: Float32Array;
}

interface ShaderProfile {
  readonly name: ShaderName;
  readonly label: string;
  readonly drawCount: number;
  readonly vertex: VertexInput | null;
  readonly uniforms: readonly UniformBinding[];
  readonly timeBinding: number | null;
}

interface PreparedRender {
  readonly animated: boolean;
  readonly draw: (elapsedSeconds: number) => void;
  readonly dispose: () => void;
}

interface ActiveRender {
  readonly dispose: () => void;
}

interface Runtime {
  readonly device: GPUDevice;
  readonly context: GPUCanvasContext;
  readonly format: GPUTextureFormat;
  lost: boolean;
  selected: ShaderProfile;
  uncapturedError: string | null;
  generation: number;
  active: ActiveRender | null;
  setup: Promise<void>;
}

const canvas = requireElement("shader-canvas", HTMLCanvasElement);
const raymarchingUniforms = createRaymarchingUniforms(
  canvas.width,
  canvas.height,
  0,
  1,
);

const profiles = [
  {
    name: "MinimumTriangleShader",
    label: "Triangle",
    drawCount: 3,
    vertex: null,
    uniforms: [],
    timeBinding: null,
  },
  {
    name: "SimpleStructUniformShaderModule",
    label: "Uniform",
    drawCount: 3,
    vertex: null,
    uniforms: [
      {
        binding: 0,
        data: new Float32Array([0.1, 0.65, 1, 1, 0.7, 0.7, 0.1, 0]),
      },
    ],
    timeBinding: null,
  },
  {
    name: "MandelbrotDistanceShaderModule",
    label: "Mandelbrot",
    drawCount: 6,
    vertex: {
      layout: fullScreenVertexLayout,
      data: fullScreenVertices,
    },
    uniforms: [{ binding: 0, data: new Float32Array([0]) }],
    timeBinding: 0,
  },
  {
    name: "RaymarchingPrimitiveShader",
    label: "Raymarching",
    drawCount: 6,
    vertex: {
      layout: fullScreenVertexLayout,
      data: fullScreenVertices,
    },
    uniforms: [
      {
        binding: 0,
        data: raymarchingUniforms.resolution,
      },
      { binding: 1, data: raymarchingUniforms.time },
      {
        binding: 2,
        data: raymarchingUniforms.mouse,
      },
      { binding: 3, data: raymarchingUniforms.antialiasing },
    ],
    timeBinding: 1,
  },
] as const satisfies readonly ShaderProfile[];

function requireElement<T extends Element>(
  id: string,
  constructor: { new (): T },
): T {
  const element = document.getElementById(id);
  if (!(element instanceof constructor)) {
    throw new Error(`Expected #${id} to be a ${constructor.name}`);
  }
  return element;
}

function requireProfile(name: string | undefined): ShaderProfile {
  const profile = profiles.find((candidate) => candidate.name === name);
  if (!profile) {
    throw new Error(`Unknown shader button '${String(name)}'.`);
  }
  return profile;
}

const buttonGroup = requireElement("shader-buttons", HTMLDivElement);
const statusOutput = requireElement("status", HTMLParagraphElement);
const adapterOutput = requireElement("adapter", HTMLPreElement);
const diagnosticsOutput = requireElement("diagnostics", HTMLPreElement);
const wgslOutput = requireElement("wgsl", HTMLPreElement);
const buttons = Array.from(
  document.querySelectorAll<HTMLButtonElement>("button[data-shader]"),
  (element) => ({
    element,
    profile: requireProfile(element.dataset.shader),
  }),
);
if (buttons.length !== profiles.length) {
  throw new Error("Expected one button for each supported shader.");
}

function describeError(error: unknown): string {
  return error instanceof Error
    ? `${error.name}: ${error.message}`
    : String(error);
}

function formatAdapterInfo(info: GPUAdapterInfo): string {
  const fields: ReadonlyArray<readonly [string, string]> = [
    ["vendor", info.vendor],
    ["architecture", info.architecture],
    ["device", info.device],
    ["description", info.description],
    ["browser fallback flag", String(info.isFallbackAdapter)],
  ];
  const availableFields = fields.filter(([, value]) => value.length > 0);

  return availableFields.length === 0
    ? "The browser exposed an adapter but no identifying information."
    : availableFields.map(([name, value]) => `${name}: ${value}`).join("\n");
}

function showStatus(message: string, error = false): void {
  statusOutput.classList.toggle("error", error);
  statusOutput.textContent = message;
}

function setBusy(busy: boolean, disabled = busy): void {
  buttonGroup.setAttribute("aria-busy", String(busy));
  for (const button of buttons) {
    button.element.disabled = disabled;
  }
}

function selectButton(selected: ShaderProfile): void {
  for (const button of buttons) {
    button.element.setAttribute(
      "aria-pressed",
      String(button.profile === selected),
    );
  }
}

async function fetchWgsl(profile: ShaderProfile): Promise<string> {
  const response = await fetch(`/ilsl/compile/${profile.name}/wgsl`);
  if (!response.ok) {
    throw new Error(
      `compiler request returned ${response.status}: ${await response.text()}`,
    );
  }
  return response.text();
}

async function withGpuErrorScopes(
  device: GPUDevice,
  action: () => Promise<void>,
): Promise<void> {
  device.pushErrorScope("out-of-memory");
  device.pushErrorScope("internal");
  device.pushErrorScope("validation");

  let actionError: unknown;
  try {
    await action();
  } catch (error: unknown) {
    actionError = error;
  }

  const gpuErrors: GPUError[] = [];
  try {
    for (let remaining = 0; remaining < 3; remaining += 1) {
      const error = await device.popErrorScope();
      if (error) {
        gpuErrors.push(error);
      }
    }
  } catch (error: unknown) {
    actionError ??= error;
  }

  if (gpuErrors.length > 0) {
    throw new Error(
      gpuErrors
        .map((error) => `WebGPU ${error.constructor.name}: ${error.message}`)
        .join("\n"),
    );
  }
  if (actionError !== undefined) {
    throw actionError;
  }
}

function createBuffer(
  device: GPUDevice,
  label: string,
  size: number,
  usage: GPUBufferUsageFlags,
  data: AllowSharedBufferSource,
): GPUBuffer {
  const buffer = device.createBuffer({ label, size, usage });
  try {
    device.queue.writeBuffer(buffer, 0, data);
    return buffer;
  } catch (error: unknown) {
    buffer.destroy();
    throw error;
  }
}

function createDrawResources(
  device: GPUDevice,
  pipeline: GPURenderPipeline,
  profile: ShaderProfile,
) {
  const buffers: GPUBuffer[] = [];
  const timeData = profile.timeBinding === null ? null : new Float32Array([0]);
  const own = (buffer: GPUBuffer): GPUBuffer => {
    buffers.push(buffer);
    return buffer;
  };

  try {
    const vertexBuffer = profile.vertex
      ? own(
          createBuffer(
            device,
            `${profile.label} vertices`,
            profile.vertex.data.byteLength,
            GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            profile.vertex.data,
          ),
        )
      : null;
    const entries = profile.uniforms.map(({ binding, data }) => {
      const initialData = binding === profile.timeBinding ? timeData : data;
      if (!initialData) {
        throw new Error(`${profile.label} time binding ${binding} is invalid.`);
      }
      const buffer = own(
        createBuffer(
          device,
          `${profile.label} uniform ${binding}`,
          Math.max(16, initialData.byteLength),
          GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
          initialData,
        ),
      );
      return { binding, resource: { buffer } };
    });
    const bindGroup =
      entries.length === 0
        ? null
        : device.createBindGroup({
            label: `${profile.label} bind group`,
            layout: pipeline.getBindGroupLayout(0),
            entries,
          });
    let time: {
      readonly data: Float32Array;
      readonly buffer: GPUBuffer;
    } | null = null;
    if (profile.timeBinding !== null) {
      const timeEntry = entries.find(
        ({ binding }) => binding === profile.timeBinding,
      );
      if (!timeData || !timeEntry) {
        throw new Error(
          `${profile.label} has no uniform at time binding ${profile.timeBinding}.`,
        );
      }
      time = { data: timeData, buffer: timeEntry.resource.buffer };
    }

    return { bindGroup, vertexBuffer, buffers, time };
  } catch (error: unknown) {
    for (const buffer of buffers) {
      buffer.destroy();
    }
    throw error;
  }
}

function createPreparedRender(
  runtime: Runtime,
  pipeline: GPURenderPipeline,
  profile: ShaderProfile,
): PreparedRender {
  const resources = createDrawResources(runtime.device, pipeline, profile);
  let disposed = false;

  return {
    animated: resources.time !== null,
    draw(elapsedSeconds: number): void {
      if (disposed) {
        throw new Error(`${profile.label} render resources are disposed.`);
      }
      if (resources.time) {
        resources.time.data[0] = elapsedSeconds;
        runtime.device.queue.writeBuffer(
          resources.time.buffer,
          0,
          resources.time.data,
        );
      }

      const encoder = runtime.device.createCommandEncoder({
        label: `${profile.label} encoder`,
      });
      const pass = encoder.beginRenderPass({
        label: `${profile.label} render pass`,
        colorAttachments: [
          {
            view: runtime.context.getCurrentTexture().createView(),
            clearValue: clearColor,
            loadOp: "clear",
            storeOp: "store",
          },
        ],
      });
      pass.setPipeline(pipeline);
      if (resources.bindGroup) {
        pass.setBindGroup(0, resources.bindGroup);
      }
      if (resources.vertexBuffer) {
        pass.setVertexBuffer(0, resources.vertexBuffer);
      }
      pass.draw(profile.drawCount);
      pass.end();
      runtime.device.queue.submit([encoder.finish()]);
    },
    dispose(): void {
      if (disposed) {
        return;
      }
      disposed = true;
      for (const buffer of resources.buffers) {
        buffer.destroy();
      }
    },
  };
}

function finishPendingOutputs(
  profile: ShaderProfile,
  receivedWgsl: boolean,
  receivedDiagnostics: boolean,
): void {
  if (!receivedWgsl) {
    wgslOutput.textContent = `No WGSL received for ${profile.name}.`;
  }
  if (!receivedDiagnostics) {
    diagnosticsOutput.textContent = `No WebGPU diagnostics produced for ${profile.name}.`;
  }
}

function isCurrentSelection(runtime: Runtime, generation: number): boolean {
  return !runtime.lost && runtime.generation === generation;
}

async function renderProfile(
  runtime: Runtime,
  profile: ShaderProfile,
  generation: number,
): Promise<PreparedRender | null> {
  let receivedWgsl = false;
  let receivedDiagnostics = false;
  let prepared: PreparedRender | null = null;

  wgslOutput.textContent = `Waiting for ${profile.name} compiler response.`;
  diagnosticsOutput.textContent = `Waiting for ${profile.name} WebGPU diagnostics.`;
  showStatus(`Compiling ${profile.label} from C#…`);

  try {
    const code = await fetchWgsl(profile);
    if (!isCurrentSelection(runtime, generation)) {
      return null;
    }
    receivedWgsl = true;
    wgslOutput.textContent = code;

    await withGpuErrorScopes(runtime.device, async () => {
      const module = runtime.device.createShaderModule({
        label: `${profile.label} generated WGSL`,
        code,
      });
      const compilationInfo = await module.getCompilationInfo();
      if (!isCurrentSelection(runtime, generation)) {
        return;
      }
      receivedDiagnostics = true;
      const diagnostics = compilationInfo.messages.map(
        (message) =>
          `${message.type} ${message.lineNum}:${message.linePos} ${message.message}`,
      );
      diagnosticsOutput.textContent =
        diagnostics.length === 0
          ? `No WebGPU compilation diagnostics for ${profile.name}.`
          : diagnostics.join("\n");

      const compilationErrors = compilationInfo.messages.filter(
        (message) => message.type === "error",
      );
      if (compilationErrors.length > 0) {
        throw new Error(
          `shader compilation errors:\n${compilationErrors
            .map(
              (message) =>
                `${message.lineNum}:${message.linePos} ${message.message}`,
            )
            .join("\n")}`,
        );
      }

      showStatus(`Creating the ${profile.label} pipeline…`);
      const pipeline = await runtime.device.createRenderPipelineAsync({
        label: `${profile.label} pipeline`,
        layout: "auto",
        vertex: {
          module,
          entryPoint: "vs",
          buffers: profile.vertex ? [profile.vertex.layout] : [],
        },
        fragment: {
          module,
          entryPoint: "fs",
          targets: [{ format: runtime.format }],
        },
        primitive: { topology: "triangle-list" },
      });
      if (!isCurrentSelection(runtime, generation)) {
        return;
      }
      const render = createPreparedRender(runtime, pipeline, profile);
      const pending: ActiveRender = { dispose: render.dispose };
      runtime.active = pending;
      try {
        render.draw(0);
        await runtime.device.queue.onSubmittedWorkDone();
        if (isCurrentSelection(runtime, generation)) {
          prepared = render;
        } else {
          if (runtime.active === pending) {
            runtime.active = null;
          }
          render.dispose();
        }
      } catch (error: unknown) {
        if (runtime.active === pending) {
          runtime.active = null;
        }
        render.dispose();
        throw error;
      }
    });
    return prepared;
  } catch (error: unknown) {
    const active = runtime.active;
    runtime.active = null;
    active?.dispose();
    if (!isCurrentSelection(runtime, generation)) {
      return null;
    }
    finishPendingOutputs(profile, receivedWgsl, receivedDiagnostics);
    throw error;
  }
}

function stopRendering(runtime: Runtime): void {
  runtime.generation += 1;
  const active = runtime.active;
  runtime.active = null;
  active?.dispose();
}

function activateRender(
  runtime: Runtime,
  profile: ShaderProfile,
  generation: number,
  render: PreparedRender,
): void {
  let animationFrame: number | null = null;
  let disposed = false;
  const active: ActiveRender = {
    dispose(): void {
      if (disposed) {
        return;
      }
      disposed = true;
      if (animationFrame !== null) {
        cancelAnimationFrame(animationFrame);
        animationFrame = null;
      }
      render.dispose();
    },
  };
  runtime.active = active;

  if (!render.animated) {
    return;
  }

  const startedAt = performance.now();
  const animate = (timestamp: number): void => {
    animationFrame = null;
    if (disposed || !isCurrentSelection(runtime, generation)) {
      return;
    }

    try {
      render.draw(Math.max(0, timestamp - startedAt) / 1000);
    } catch (error: unknown) {
      const current =
        runtime.active === active && isCurrentSelection(runtime, generation);
      active.dispose();
      if (runtime.active === active) {
        runtime.active = null;
      }
      if (current) {
        runtime.generation += 1;
        canvas.hidden = true;
        setBusy(false);
        showStatus(
          `${profile.label} (${profile.name}) failed: ${describeError(error)}`,
          true,
        );
      }
      console.error(error);
      return;
    }

    if (!disposed && isCurrentSelection(runtime, generation)) {
      animationFrame = requestAnimationFrame(animate);
    }
  };
  animationFrame = requestAnimationFrame(animate);
}

async function initialize(): Promise<Runtime> {
  if (!navigator.gpu) {
    throw new Error("WebGPU is not available in this browser.");
  }

  showStatus("Requesting the browser's default WebGPU adapter…");
  const adapter = await navigator.gpu.requestAdapter();
  if (!adapter) {
    throw new Error("The browser did not provide a WebGPU adapter.");
  }
  adapterOutput.textContent = formatAdapterInfo(adapter.info);

  const device = await adapter.requestDevice();
  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error("Failed to create a WebGPU canvas context.");
  }
  const format = navigator.gpu.getPreferredCanvasFormat();
  context.configure({ device, format, alphaMode: "opaque" });

  const runtime: Runtime = {
    device,
    context,
    format,
    lost: false,
    selected: profiles[0],
    uncapturedError: null,
    generation: 0,
    active: null,
    setup: Promise.resolve(),
  };
  device.onuncapturederror = (event) => {
    console.error(event.error);
    if (runtime.lost) {
      return;
    }
    runtime.uncapturedError = `${runtime.selected.name}: WebGPU uncaptured error: ${event.error.message}`;
    stopRendering(runtime);
    canvas.hidden = true;
    setBusy(false);
    showStatus(runtime.uncapturedError, true);
  };
  void device.lost.then((loss) => {
    runtime.lost = true;
    stopRendering(runtime);
    canvas.hidden = true;
    setBusy(false, true);
    showStatus(
      `${runtime.selected.name}: WebGPU device lost (${loss.reason}): ${loss.message}`,
      true,
    );
  });
  return runtime;
}

async function main(): Promise<void> {
  setBusy(true);
  const runtime = await initialize();

  const select = async (profile: ShaderProfile): Promise<void> => {
    if (runtime.lost) {
      return;
    }

    stopRendering(runtime);
    const generation = runtime.generation;
    runtime.selected = profile;
    runtime.uncapturedError = null;
    canvas.hidden = true;
    selectButton(profile);
    setBusy(true);
    const renderPromise = runtime.setup.then(() =>
      isCurrentSelection(runtime, generation)
        ? renderProfile(runtime, profile, generation)
        : null,
    );
    runtime.setup = renderPromise.then(
      () => undefined,
      () => undefined,
    );
    try {
      const render = await renderPromise;
      if (!render || !isCurrentSelection(runtime, generation)) {
        render?.dispose();
        return;
      }
      if (runtime.uncapturedError) {
        render.dispose();
        showStatus(runtime.uncapturedError, true);
        return;
      }

      canvas.hidden = false;
      setBusy(false);
      showStatus(
        render.animated
          ? `Animating ${profile.label} (${profile.name}).`
          : `${profile.label} (${profile.name}) rendered one frame successfully.`,
      );
      activateRender(runtime, profile, generation, render);
    } catch (error: unknown) {
      if (isCurrentSelection(runtime, generation)) {
        stopRendering(runtime);
        canvas.hidden = true;
        setBusy(false);
        showStatus(
          `${profile.label} (${profile.name}) failed: ${describeError(error)}`,
          true,
        );
      }
      console.error(error);
    } finally {
      if (isCurrentSelection(runtime, generation)) {
        setBusy(false);
      }
    }
  };

  for (const button of buttons) {
    button.element.addEventListener("click", () => {
      void select(button.profile);
    });
  }
  window.addEventListener("pagehide", () => {
    stopRendering(runtime);
    if (!runtime.lost) {
      setBusy(false);
    }
  });
  window.addEventListener("pageshow", (event) => {
    if (event.persisted && !runtime.lost) {
      void select(runtime.selected);
    }
  });
  await select(profiles[0]);
}

void main().catch((error: unknown) => {
  setBusy(false, true);
  canvas.hidden = true;
  wgslOutput.textContent =
    "No WGSL requested because WebGPU initialization failed.";
  diagnosticsOutput.textContent =
    "No WebGPU diagnostics produced because initialization failed.";
  showStatus(describeError(error), true);
  console.error(error);
});
