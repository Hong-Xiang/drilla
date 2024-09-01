import { GUI } from "lil-gui";
import { createRoot } from "react-dom/client";
import { InteractiveApp } from "./interactive-ui";
import { createElement } from "react";

interface InteractiveState {
  loop: boolean;
}

interface RealtimeState {
  color: {
    r: number;
    g: number;
    b: number;
  };
}

export async function BatchRenderMain() {
  const canvas = document.getElementById("render-root") as
    | HTMLCanvasElement
    | undefined;
  if (!canvas || !(canvas instanceof HTMLCanvasElement)) {
    throw Error("Failed to get canvas");
  }

  const ui = document.getElementById("ui-root") as HTMLDivElement | undefined;
  if (!ui || !(ui instanceof HTMLDivElement)) {
    throw Error("Failed to get ui root");
  }

  const format = navigator.gpu.getPreferredCanvasFormat();
  const adapter = await navigator.gpu.requestAdapter();
  const device = await adapter?.requestDevice();
  if (!device) {
    throw new Error(`Failed to request adapter`);
  }

  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error(`Failed to get webgpu context`);
  }

  context.configure({
    device,
    format,
    alphaMode: "opaque",
  });

  let interactiveState: InteractiveState = {
    loop: false,
  };
  let needOneTimeRender = true;
  const realtimeState: RealtimeState = {
    color: { r: 0.1, g: 0.3, b: 0.3 },
  };

  createInteractiveUserInterface(ui, interactiveState, (s) => {
    interactiveState = s;
  });
  createRealtimeUserInterface(realtimeState);

  const module = device.createShaderModule({
    label: "our hardcoded red triangle shaders",
    code: `
      @vertex fn vs(
        @builtin(vertex_index) vertexIndex : u32
      ) -> @builtin(position) vec4f {
        var pos = array(
          vec2f( 0.0,  0.5),  // top center
          vec2f(-0.5, -0.5),  // bottom left
          vec2f( 0.5, -0.5)   // bottom right
        );
 
        return vec4f(pos[vertexIndex], 0.0, 1.0);
      }
 
      @fragment fn fs() -> @location(0) vec4f {
        return vec4f(1.0, 1.0, 0.5, 1.0);
      }
    `,
  });

  const pipeline = device.createRenderPipeline({
    layout: "auto",
    vertex: {
      module: module,
    },
    fragment: {
      module,
      targets: [
        {
          format,
        },
      ],
    },
    primitive: {
      topology: "triangle-list",
    },
  });

  const render = (f: number) => {
    if (!interactiveState.loop && !needOneTimeRender) {
      console.log("skip rendering");
      requestAnimationFrame(render);
      return;
    }
    needOneTimeRender = false;

    const view = context.getCurrentTexture().createView();

    const encoder = device.createCommandEncoder();

    clearCanvas(encoder, view, realtimeState.color);

    const renderPassDescriptor: GPURenderPassDescriptor = {
      label: "our basic canvas renderPass",
      colorAttachments: [
        {
          view,
          loadOp: "load",
          storeOp: "store",
        },
      ],
    };

    const pass = encoder.beginRenderPass(renderPassDescriptor);
    pass.setPipeline(pipeline);
    pass.draw(3);
    pass.end();

    device.queue.submit([encoder.finish()]);

    requestAnimationFrame(render);
  };

  requestAnimationFrame(render);
}

function createInteractiveUserInterface(
  uiRoot: HTMLDivElement,
  initState: InteractiveState,
  update: (s: InteractiveState) => void
) {
  const root = createRoot(uiRoot);
  root.render(
    createElement(InteractiveApp, {
      state: { loop: initState.loop },
      update,
    })
  );
}

function createRealtimeUserInterface(state: RealtimeState) {
  const gui = new GUI();
  gui.addColor(state, "color");
  gui.onChange(({ object }) => {
    console.log(object);
  });
}

function clearCanvas(
  encoder: GPUCommandEncoder,
  target: GPUTextureView,
  color: { r: number; g: number; b: number }
) {
  const pass = encoder.beginRenderPass({
    label: "clear-pass",
    colorAttachments: [
      {
        view: target,
        clearValue: [color.r, color.g, color.b, 1],
        loadOp: "clear",
        storeOp: "store",
      },
    ],
  });
  pass.end();
}
