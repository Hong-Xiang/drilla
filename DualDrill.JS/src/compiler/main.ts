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

function describeError(error: unknown): string {
  return error instanceof Error
    ? `${error.name}: ${error.message}`
    : String(error);
}

function formatAdapterInfo(
  info: GPUAdapterInfo,
  isFallbackAdapter: boolean,
): string {
  const fields: ReadonlyArray<readonly [string, string]> = [
    ["vendor", info.vendor],
    ["architecture", info.architecture],
    ["device", info.device],
    ["description", info.description],
    ["browser fallback flag", String(isFallbackAdapter)],
  ];
  const availableFields = fields.filter(([, value]) => value.length > 0);

  return availableFields.length === 0
    ? "The browser exposed an adapter but no identifying information."
    : availableFields.map(([name, value]) => `${name}: ${value}`).join("\n");
}

async function fetchWgsl(): Promise<string> {
  const response = await fetch("/ilsl/compile/MinimumTriangleShader/wgsl");
  if (!response.ok) {
    throw new Error(
      `Compiler request failed (${response.status}): ${await response.text()}`,
    );
  }
  return response.text();
}

async function render(): Promise<void> {
  const status = requireElement("status", HTMLParagraphElement);
  const adapterOutput = requireElement("adapter", HTMLPreElement);
  const diagnosticsOutput = requireElement("diagnostics", HTMLPreElement);
  const wgslOutput = requireElement("wgsl", HTMLPreElement);
  const canvas = requireElement("triangle", HTMLCanvasElement);

  if (!navigator.gpu) {
    throw new Error("WebGPU is not available in this browser.");
  }

  status.textContent = "Compiling C# shader…";
  const code = await fetchWgsl();
  wgslOutput.textContent = code;

  status.textContent = "Requesting the browser's default WebGPU adapter…";
  const adapter = await navigator.gpu.requestAdapter();
  if (!adapter) {
    throw new Error("The browser did not provide a WebGPU adapter.");
  }
  const adapterInfo = await adapter.requestAdapterInfo();
  adapterOutput.textContent = formatAdapterInfo(
    adapterInfo,
    adapter.isFallbackAdapter,
  );

  const device = await adapter.requestDevice();
  device.onuncapturederror = (event) => {
    status.classList.add("error");
    status.textContent = `WebGPU error: ${event.error.message}`;
  };
  void device.lost.then((loss) => {
    status.classList.add("error");
    status.textContent = `WebGPU device lost (${loss.reason}): ${loss.message}`;
  });

  const module = device.createShaderModule({
    label: "C# generated triangle",
    code,
  });
  const compilationInfo = await module.getCompilationInfo();
  diagnosticsOutput.textContent =
    compilationInfo.messages.length === 0
      ? "No WebGPU compilation diagnostics."
      : compilationInfo.messages
          .map(
            (message) =>
              `${message.type} ${message.lineNum}:${message.linePos} ${message.message}`,
          )
          .join("\n");

  if (compilationInfo.messages.some((message) => message.type === "error")) {
    throw new Error("Generated WGSL contains WebGPU compilation errors.");
  }

  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error("Failed to create a WebGPU canvas context.");
  }

  const format = navigator.gpu.getPreferredCanvasFormat();
  context.configure({ device, format, alphaMode: "opaque" });

  status.textContent = "Creating render pipeline…";
  const pipeline = await device.createRenderPipelineAsync({
    label: "C# generated triangle pipeline",
    layout: "auto",
    vertex: { module, entryPoint: "vs" },
    fragment: { module, entryPoint: "fs", targets: [{ format }] },
    primitive: { topology: "triangle-list" },
  });

  const encoder = device.createCommandEncoder({ label: "triangle encoder" });
  const pass = encoder.beginRenderPass({
    label: "triangle render pass",
    colorAttachments: [
      {
        view: context.getCurrentTexture().createView(),
        clearValue: { r: 0.03, g: 0.21, b: 0.26, a: 1 },
        loadOp: "clear",
        storeOp: "store",
      },
    ],
  });
  pass.setPipeline(pipeline);
  pass.draw(3);
  pass.end();
  device.queue.submit([encoder.finish()]);

  status.textContent = "Rendered generated WGSL successfully.";
}

render().catch((error: unknown) => {
  const message = describeError(error);
  const status = document.getElementById("status");
  if (status) {
    status.classList.add("error");
    status.textContent = message;
  }
  console.error(error);
});
