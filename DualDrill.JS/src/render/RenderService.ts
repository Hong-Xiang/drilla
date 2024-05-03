import { Subject, animationFrameScheduler, observeOn } from "rxjs";

export async function createWebGPURenderService(
  canvas: HTMLCanvasElement
): Promise<RenderRoot> {
  const adapter = await navigator.gpu.requestAdapter();
  const device = await adapter?.requestDevice();
  if (!device) {
    throw new Error(`Failed to request adapter`);
  }
  const presentationFormat = navigator.gpu.getPreferredCanvasFormat();

  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error(`Failed to get webgpu context`);
  }

  context.configure({
    device,
    format: presentationFormat,
  });

  const module = device.createShaderModule({
    label: "our hardcoded red triangle shaders",
    code: `
      @vertex fn vs(
        @builtin(vertex_index) vertexIndex : u32
      ) -> @builtin(position) vec4f {
        let pos = array(
          vec2f( 0.0,  0.5),  // top center
          vec2f(-0.5, -0.5),  // bottom left
          vec2f( 0.5, -0.5)   // bottom right
        );
 
        return vec4f(pos[vertexIndex], 0.0, 1.0);
      }
 
      @fragment fn fs() -> @location(0) vec4f {
        return vec4f(1.0, 0.0, 0.0, 1.0);
      }
    `,
  });

  const pipeline = device.createRenderPipeline({
    label: "our hardcoded red triangle pipeline",
    layout: "auto",
    vertex: {
      module,
      entryPoint: "vs",
    },
    fragment: {
      module,
      entryPoint: "fs",
      targets: [{ format: presentationFormat }],
    },
  });

  const result: RenderRoot = {
    render(f, scale) {
      const t = f / 100;

      // Get the current texture from the canvas context and
      // set it as the texture to render to.
      const renderPassDescriptor: GPURenderPassDescriptor = {
        label: "our basic canvas renderPass",
        colorAttachments: [
          {
            view: context.getCurrentTexture().createView(),
            clearValue: [Math.sin(t) * 2 + 1, Math.cos(t) * 2 + 1, 0.3, 1],
            loadOp: "clear",
            storeOp: "store",
          },
        ],
      };

      // make a command encoder to start encoding commands
      const encoder = device.createCommandEncoder({ label: "our encoder" });

      // make a render pass encoder to encode render specific commands
      const pass = encoder.beginRenderPass(renderPassDescriptor);
      const h = context.getCurrentTexture().height;
      const w = context.getCurrentTexture().width;
      const hs = h / scale;
      const ws = w / scale;

      pass.setViewport((w - ws) / 2, (h - hs) / 2, ws, hs, 0, 1);
      pass.setPipeline(pipeline);
      pass.draw(3); // call our vertex shader 3 times
      pass.end();

      const commandBuffer = encoder.finish();
      device.queue.submit([commandBuffer]);
    },
  };

  const renderStates = new Subject<{
    time: number,
    scale: number
  }>();

  renderStates.pipe(observeOn(animationFrameScheduler)).subscribe({
    next: ({ time, scale }) => {
      result.render(time, scale);
    },
    error: (e) => {
      console.error(e);
    },
  });

  return {
    render: (t, scale) => {
      renderStates.next({ time: t, scale });
    },
  };
}
export interface RenderGraph { }
export interface RenderState { }
export interface RenderRoot {
  render(t: number, scale: number): void;
}