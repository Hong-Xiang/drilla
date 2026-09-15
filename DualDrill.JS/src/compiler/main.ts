const canvasWidth = 800;
const canvasHeight = 600;
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

type ShaderProfile =
  | {
      readonly name: "MinimumTriangleShader";
      readonly label: "Triangle";
      readonly kind: "triangle";
    }
  | {
      readonly name: "SimpleStructUniformShaderModule";
      readonly label: "Uniform";
      readonly kind: "uniform";
    }
  | {
      readonly name: "MandelbrotDistanceShaderModule";
      readonly label: "Mandelbrot";
      readonly kind: "mandelbrot";
    }
  | {
      readonly name: "RaymarchingPrimitiveShader";
      readonly label: "Raymarching";
      readonly kind: "raymarching";
    };

interface Runtime {
  readonly device: GPUDevice;
  readonly context: GPUCanvasContext;
  readonly format: GPUTextureFormat;
  readonly state: {
    lost: boolean;
    selected: ShaderName;
    uncapturedError: string | null;
  };
}

interface DrawResources {
  readonly bindGroup: GPUBindGroup | null;
  readonly vertexBuffer: GPUBuffer | null;
  readonly buffers: readonly GPUBuffer[];
}

interface Outputs {
  readonly status: HTMLParagraphElement;
  readonly adapter: HTMLPreElement;
  readonly diagnostics: HTMLPreElement;
  readonly wgsl: HTMLPreElement;
}

interface ShaderButton {
  readonly element: HTMLButtonElement;
  readonly name: ShaderName;
}

const profiles: Readonly<Record<ShaderName, ShaderProfile>> = {
  MinimumTriangleShader: {
    name: "MinimumTriangleShader",
    label: "Triangle",
    kind: "triangle",
  },
  SimpleStructUniformShaderModule: {
    name: "SimpleStructUniformShaderModule",
    label: "Uniform",
    kind: "uniform",
  },
  MandelbrotDistanceShaderModule: {
    name: "MandelbrotDistanceShaderModule",
    label: "Mandelbrot",
    kind: "mandelbrot",
  },
  RaymarchingPrimitiveShader: {
    name: "RaymarchingPrimitiveShader",
    label: "Raymarching",
    kind: "raymarching",
  },
};

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

function parseShaderName(value: string | undefined): ShaderName {
  switch (value) {
    case "MinimumTriangleShader":
    case "SimpleStructUniformShaderModule":
    case "MandelbrotDistanceShaderModule":
    case "RaymarchingPrimitiveShader":
      return value;
    default:
      throw new Error(`Unknown shader button '${String(value)}'.`);
  }
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

function setError(status: HTMLParagraphElement, message: string): void {
  status.classList.add("error");
  status.textContent = message;
}

function setStatus(status: HTMLParagraphElement, message: string): void {
  status.classList.remove("error");
  status.textContent = message;
}

function setButtonsDisabled(
  buttons: readonly ShaderButton[],
  disabled: boolean,
): void {
  for (const button of buttons) {
    button.element.disabled = disabled;
  }
}

function selectButton(
  buttons: readonly ShaderButton[],
  selected: ShaderName,
): void {
  for (const button of buttons) {
    button.element.setAttribute(
      "aria-pressed",
      String(button.name === selected),
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

async function withGpuErrorScopes<T>(
  device: GPUDevice,
  action: () => Promise<T>,
): Promise<T> {
  device.pushErrorScope("out-of-memory");
  device.pushErrorScope("internal");
  device.pushErrorScope("validation");

  let outcome: { readonly value: T } | null = null;
  let actionError: unknown;
  try {
    outcome = { value: await action() };
  } catch (error: unknown) {
    actionError = error;
  }

  const gpuErrors: GPUError[] = [];
  try {
    const validationError = await device.popErrorScope();
    const internalError = await device.popErrorScope();
    const outOfMemoryError = await device.popErrorScope();
    for (const error of [validationError, internalError, outOfMemoryError]) {
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
  if (!outcome) {
    throw new Error("WebGPU operation returned no result.");
  }
  return outcome.value;
}

function createBuffer(
  device: GPUDevice,
  label: string,
  size: number,
  usage: GPUBufferUsageFlags,
  data: Float32Array,
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
): DrawResources {
  const buffers: GPUBuffer[] = [];
  const own = (buffer: GPUBuffer): GPUBuffer => {
    buffers.push(buffer);
    return buffer;
  };

  try {
    switch (profile.kind) {
      case "triangle":
        return { bindGroup: null, vertexBuffer: null, buffers };
      case "uniform": {
        const uniformBuffer = own(
          createBuffer(
            device,
            `${profile.label} uniform`,
            32,
            GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            new Float32Array([0.1, 0.65, 1, 1, 0.7, 0.7, 0.1, 0]),
          ),
        );
        return {
          bindGroup: device.createBindGroup({
            label: `${profile.label} bind group`,
            layout: pipeline.getBindGroupLayout(0),
            entries: [{ binding: 0, resource: { buffer: uniformBuffer } }],
          }),
          vertexBuffer: null,
          buffers,
        };
      }
      case "mandelbrot": {
        const vertexBuffer = own(
          createBuffer(
            device,
            `${profile.label} vertices`,
            fullScreenVertices.byteLength,
            GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            fullScreenVertices,
          ),
        );
        const timeBuffer = own(
          createBuffer(
            device,
            `${profile.label} time`,
            16,
            GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            new Float32Array([0]),
          ),
        );
        return {
          bindGroup: device.createBindGroup({
            label: `${profile.label} bind group`,
            layout: pipeline.getBindGroupLayout(0),
            entries: [{ binding: 0, resource: { buffer: timeBuffer } }],
          }),
          vertexBuffer,
          buffers,
        };
      }
      case "raymarching": {
        const vertexBuffer = own(
          createBuffer(
            device,
            `${profile.label} vertices`,
            fullScreenVertices.byteLength,
            GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            fullScreenVertices,
          ),
        );
        const resolutionBuffer = own(
          createBuffer(
            device,
            `${profile.label} resolution`,
            16,
            GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            new Float32Array([canvasWidth, canvasHeight]),
          ),
        );
        const timeBuffer = own(
          createBuffer(
            device,
            `${profile.label} time`,
            16,
            GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            new Float32Array([0]),
          ),
        );
        return {
          bindGroup: device.createBindGroup({
            label: `${profile.label} bind group`,
            layout: pipeline.getBindGroupLayout(0),
            entries: [
              { binding: 0, resource: { buffer: resolutionBuffer } },
              { binding: 1, resource: { buffer: timeBuffer } },
            ],
          }),
          vertexBuffer,
          buffers,
        };
      }
    }
  } catch (error: unknown) {
    for (const buffer of buffers) {
      buffer.destroy();
    }
    throw error;
  }
}

function clearCanvas(runtime: Runtime, label: string): void {
  const encoder = runtime.device.createCommandEncoder({
    label: `${label} clear encoder`,
  });
  const pass = encoder.beginRenderPass({
    label: `${label} clear pass`,
    colorAttachments: [
      {
        view: runtime.context.getCurrentTexture().createView(),
        clearValue: clearColor,
        loadOp: "clear",
        storeOp: "store",
      },
    ],
  });
  pass.end();
  runtime.device.queue.submit([encoder.finish()]);
}

function vertexBuffers(
  profile: ShaderProfile,
): readonly GPUVertexBufferLayout[] {
  switch (profile.kind) {
    case "triangle":
    case "uniform":
      return [];
    case "mandelbrot":
    case "raymarching":
      return [fullScreenVertexLayout];
  }
}

async function renderProfile(
  runtime: Runtime,
  profile: ShaderProfile,
  outputs: Outputs,
): Promise<void> {
  outputs.wgsl.textContent = `Waiting for ${profile.name} compiler response.`;
  outputs.diagnostics.textContent = `Waiting for ${profile.name} WebGPU diagnostics.`;
  setStatus(outputs.status, `Clearing the canvas for ${profile.label}…`);
  await withGpuErrorScopes(runtime.device, async () => {
    clearCanvas(runtime, profile.label);
    await runtime.device.queue.onSubmittedWorkDone();
  });

  setStatus(outputs.status, `Compiling ${profile.label} from C#…`);
  const code = await fetchWgsl(profile);
  outputs.wgsl.textContent = code;

  const ownedBuffers: GPUBuffer[] = [];
  try {
    await withGpuErrorScopes(runtime.device, async () => {
      const module = runtime.device.createShaderModule({
        label: `${profile.label} generated WGSL`,
        code,
      });
      const compilationInfo = await module.getCompilationInfo();
      const diagnostics = compilationInfo.messages.map(
        (message) =>
          `${message.type} ${message.lineNum}:${message.linePos} ${message.message}`,
      );
      outputs.diagnostics.textContent =
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

      setStatus(outputs.status, `Creating the ${profile.label} pipeline…`);
      const pipeline = await runtime.device.createRenderPipelineAsync({
        label: `${profile.label} pipeline`,
        layout: "auto",
        vertex: {
          module,
          entryPoint: "vs",
          buffers: vertexBuffers(profile),
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
      pass.draw(
        profile.kind === "triangle" || profile.kind === "uniform" ? 3 : 6,
      );
      pass.end();
      runtime.device.queue.submit([encoder.finish()]);
      await runtime.device.queue.onSubmittedWorkDone();
    });
  } finally {
    for (const buffer of ownedBuffers) {
      buffer.destroy();
    }
  }
}

async function initialize(
  buttons: readonly ShaderButton[],
  outputs: Outputs,
): Promise<Runtime> {
  if (!navigator.gpu) {
    throw new Error("WebGPU is not available in this browser.");
  }

  setStatus(outputs.status, "Requesting the browser's default WebGPU adapter…");
  const adapter = await navigator.gpu.requestAdapter();
  if (!adapter) {
    throw new Error("The browser did not provide a WebGPU adapter.");
  }
  const adapterInfo = adapter.info;
  outputs.adapter.textContent = formatAdapterInfo(adapterInfo);

  const device = await adapter.requestDevice();
  const canvas = requireElement("shader-canvas", HTMLCanvasElement);
  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error("Failed to create a WebGPU canvas context.");
  }
  const format = navigator.gpu.getPreferredCanvasFormat();
  context.configure({ device, format, alphaMode: "opaque" });

  const state: Runtime["state"] = {
    lost: false,
    selected: "MinimumTriangleShader",
    uncapturedError: null,
  };
  device.onuncapturederror = (event) => {
    state.uncapturedError = `${state.selected}: WebGPU uncaptured error: ${event.error.message}`;
    setError(outputs.status, state.uncapturedError);
    console.error(event.error);
  };
  void device.lost.then((loss) => {
    state.lost = true;
    setButtonsDisabled(buttons, true);
    setError(
      outputs.status,
      `${state.selected}: WebGPU device lost (${loss.reason}): ${loss.message}`,
    );
  });

  return { device, context, format, state };
}

async function main(): Promise<void> {
  const outputs: Outputs = {
    status: requireElement("status", HTMLParagraphElement),
    adapter: requireElement("adapter", HTMLPreElement),
    diagnostics: requireElement("diagnostics", HTMLPreElement),
    wgsl: requireElement("wgsl", HTMLPreElement),
  };
  const buttons = Array.from(
    document.querySelectorAll<HTMLButtonElement>("button[data-shader]"),
    (element): ShaderButton => ({
      element,
      name: parseShaderName(element.dataset.shader),
    }),
  );
  if (buttons.length !== Object.keys(profiles).length) {
    throw new Error("Expected one button for each supported shader.");
  }

  setButtonsDisabled(buttons, true);
  const runtime = await initialize(buttons, outputs);

  const select = async (name: ShaderName): Promise<void> => {
    if (runtime.state.lost) {
      return;
    }

    const profile = profiles[name];
    runtime.state.selected = name;
    selectButton(buttons, name);
    setButtonsDisabled(buttons, true);
    try {
      await renderProfile(runtime, profile, outputs);
      if (runtime.state.uncapturedError) {
        setError(outputs.status, runtime.state.uncapturedError);
      } else {
        setStatus(
          outputs.status,
          `${profile.label} (${profile.name}) rendered one frame successfully.`,
        );
      }
    } catch (error: unknown) {
      setError(
        outputs.status,
        `${profile.label} (${profile.name}) failed: ${describeError(error)}`,
      );
      console.error(error);
    } finally {
      if (!runtime.state.lost) {
        setButtonsDisabled(buttons, false);
      }
    }
  };

  for (const button of buttons) {
    button.element.addEventListener("click", () => {
      void select(button.name);
    });
  }
  await select("MinimumTriangleShader");
}

main().catch((error: unknown) => {
  const message = describeError(error);
  const status = document.getElementById("status");
  if (status) {
    status.classList.add("error");
    status.textContent = message;
  }
  console.error(error);
});
