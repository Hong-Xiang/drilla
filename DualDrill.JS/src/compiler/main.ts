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
}

interface Runtime {
  readonly device: GPUDevice;
  readonly context: GPUCanvasContext;
  readonly format: GPUTextureFormat;
  lost: boolean;
  selected: ShaderProfile;
  uncapturedError: string | null;
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
      const buffer = own(
        createBuffer(
          device,
          `${profile.label} uniform ${binding}`,
          Math.max(16, data.byteLength),
          GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
          data,
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

    return { bindGroup, vertexBuffer, buffers };
  } catch (error: unknown) {
    for (const buffer of buffers) {
      buffer.destroy();
    }
    throw error;
  }
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

async function renderProfile(
  runtime: Runtime,
  profile: ShaderProfile,
): Promise<void> {
  let receivedWgsl = false;
  let receivedDiagnostics = false;
  const ownedBuffers: GPUBuffer[] = [];

  wgslOutput.textContent = `Waiting for ${profile.name} compiler response.`;
  diagnosticsOutput.textContent = `Waiting for ${profile.name} WebGPU diagnostics.`;
  showStatus(`Compiling ${profile.label} from C#…`);

  try {
    const code = await fetchWgsl(profile);
    receivedWgsl = true;
    wgslOutput.textContent = code;

    await withGpuErrorScopes(runtime.device, async () => {
      const module = runtime.device.createShaderModule({
        label: `${profile.label} generated WGSL`,
        code,
      });
      const compilationInfo = await module.getCompilationInfo();
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
      const resources = createDrawResources(runtime.device, pipeline, profile);
      ownedBuffers.push(...resources.buffers);

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
      await runtime.device.queue.onSubmittedWorkDone();
    });
  } catch (error: unknown) {
    finishPendingOutputs(profile, receivedWgsl, receivedDiagnostics);
    throw error;
  } finally {
    for (const buffer of ownedBuffers) {
      buffer.destroy();
    }
  }
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
  };
  device.onuncapturederror = (event) => {
    console.error(event.error);
    if (runtime.lost) {
      return;
    }
    runtime.uncapturedError = `${runtime.selected.name}: WebGPU uncaptured error: ${event.error.message}`;
    canvas.hidden = true;
    showStatus(runtime.uncapturedError, true);
  };
  void device.lost.then((loss) => {
    runtime.lost = true;
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

    runtime.selected = profile;
    runtime.uncapturedError = null;
    canvas.hidden = true;
    selectButton(profile);
    setBusy(true);
    try {
      await renderProfile(runtime, profile);
      if (runtime.lost) {
        return;
      }
      if (runtime.uncapturedError) {
        showStatus(runtime.uncapturedError, true);
        return;
      }

      canvas.hidden = false;
      showStatus(
        `${profile.label} (${profile.name}) rendered one frame successfully.`,
      );
    } catch (error: unknown) {
      if (!runtime.lost) {
        showStatus(
          `${profile.label} (${profile.name}) failed: ${describeError(error)}`,
          true,
        );
      }
      console.error(error);
    } finally {
      if (!runtime.lost) {
        setBusy(false);
      }
    }
  };

  for (const button of buttons) {
    button.element.addEventListener("click", () => {
      void select(button.profile);
    });
  }
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
