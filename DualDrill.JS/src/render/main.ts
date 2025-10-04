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
    loop: true,
  };
  let needOneTimeRender = true;
  const realtimeState: RealtimeState = {
    color: { r: 0.1, g: 0.3, b: 0.3 },
  };

  createInteractiveUserInterface(ui, interactiveState, (s) => {
    interactiveState = s;
  });
  createRealtimeUserInterface(realtimeState);

  // const shaderName = "SampleFragmentShader";
  // const isUniformTest = true;
  // const shaderName = isUniformTest
  //   ? "SimpleUniformShader"
  //   : "SampleFragmentShader";
  //const code = await (await fetch(`/ilsl/wgsl/QuadShader`)).text();

  const shaderName = "RaymarchingPrimitiveShader";
  // const shaderName = "MandelbrotDistanceShader";
  const meshName = "ScreenQuad";
  // const vertexBufferLayout = await (
  //   await fetch(`/ilsl/wgsl/vertexbufferlayout/${shaderName}`)
  // ).json();
  // const vertexBufferLayout = JSON.parse(vertexBufferLayoutJson);
  const code = await (await fetch(`/ilsl/compile/${shaderName}/wgsl`)).text();
  const meshVertices = await (
    await fetch(`/api/Mesh/${meshName}/vertex`)
  ).arrayBuffer(); //Quad
  const meshIndices = await (
    await fetch(`/api/Mesh/${meshName}/index`)
  ).arrayBuffer(); //Quad
  // const bindGroupLayoutDescriptor = await (
  //   await fetch(`/ilsl/wgsl/bindgrouplayoutdescriptorbuffer/${shaderName}`)
  // ).json();

  // Uniform Buffer to pass resolution
  const resolutionBufferSize = 16; // used 8
  const resolutionBuffer = device.createBuffer({
    size: resolutionBufferSize,
    usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
  });
  const resolution = new Float32Array([1920, 1080]);
  device.queue.writeBuffer(resolutionBuffer, 0, resolution);

  // Uniform Buffer to pass time
  const timeBufferSize = 16; // used 4
  const timeBuffer = device.createBuffer({
    size: timeBufferSize,
    usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
  });
  const time = new Float32Array([0, 0, 0, 0]);
  device.queue.writeBuffer(timeBuffer, 0, time);

  // // Vertex Buffer
  const verticesBufferSize = meshVertices.byteLength;
  const vertexBuffer = device.createBuffer({
    size: verticesBufferSize,
    usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
  });
  const vertices = new Float32Array(meshVertices);
  device.queue.writeBuffer(vertexBuffer, 0, vertices);

  // Index Buffer
  const indexBufferSize = meshIndices.byteLength;
  const indexBuffer = device.createBuffer({
    size: indexBufferSize,
    usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST,
  });
  const indices = new Uint16Array(meshIndices);
  device.queue.writeBuffer(indexBuffer, 0, indices);

  // const minimumTriangleCode = await (await fetch("/ilsl/compile")).text();

  const module = device.createShaderModule({
    label: shaderName,
    // code,
    // code: minimumTriangleCode,
    code: code,
    // code : debugCode
    //     code: `
    // @vertex
    // fn vs (@builtin (vertex_index) vertexIndex:u32) -> @builtin(position) vec4<f32>
    // {
    // 	var x:f32 = f32(i32((1 - vertexIndex))) * 0.5f;
    // 	var y:f32 = f32(i32(((vertexIndex & 1) * 2 - 1))) * 0.5f;
    // 	return vec4<f32> (x, y, 0f, 1f);
    // }

    // @fragment
    // fn fs () ->@location (0)
    //  vec4<f32>
    // {
    // 	return vec4<f32> (1f, 1f, 0.5f, 1f);
    // }
    // `
    // code: `
    //   @vertex fn vs(
    //     @builtin(vertex_index) vertex_index : u32
    //   ) -> @builtin(position) vec4f {
    //     let x = f32(1 - i32(vertex_index)) * 0.5;
    //     let y = f32(i32(vertex_index & 1u) * 2 - 1) * 0.5;
    //     return vec4f(x, y, 0.0, 1.0);
    //   }

    //   @fragment fn fs() -> @location(0) vec4f {
    //     return vec4f(1.0, 1.0, 0.5, 1.0);
    //   }
    // `,
  });

  const bindGroupLayout = device.createBindGroupLayout(
    {
      entries: [
        {
          binding: 0,
          visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
          buffer: {
            type: "uniform",
          },
        },
        // {
        //   binding: 1,
        //   visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
        //   buffer: {
        //     type: "uniform",
        //   },
        // },
      ],
    }
    // bindGroupLayoutDescriptor
  );
  const bindGroup = device.createBindGroup({
    layout: bindGroupLayout,
    entries: [{ binding: 0, resource: { buffer: timeBuffer } }],
    // entries: [
    //   { binding: 0, resource: { buffer: resolutionBuffer } },
    //   { binding: 1, resource: { buffer: timeBuffer } },
    // ],
  });

  const pipelineLayout = device.createPipelineLayout({
    bindGroupLayouts: [
      bindGroupLayout, // @group(0)
    ],
  });

  const pipeline = device.createRenderPipeline({
    layout: pipelineLayout,
    // layout: "auto",
    vertex: {
      module: module,
      entryPoint: "vs",
      buffers: [
        {
          arrayStride: 2 * 4,
          stepMode: "vertex",
          attributes: [
            {
              shaderLocation: 0,
              offset: 0,
              format: "float32x2",
            },
          ],
        },
      ],
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
    const aspect = canvas.width / canvas.height;

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
    var seconds = performance.now() / 1000;
    time.set([seconds, seconds]);
    device.queue.writeBuffer(timeBuffer, 0, time);
    const pass = encoder.beginRenderPass(renderPassDescriptor);
    pass.setPipeline(pipeline);
    pass.setBindGroup(0, bindGroup!);
    pass.setVertexBuffer(0, vertexBuffer);
    pass.setIndexBuffer(indexBuffer, "uint16");
    pass.drawIndexed(indices.length);
    // pass.draw(3);
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
