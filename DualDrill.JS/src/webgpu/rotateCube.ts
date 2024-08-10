import { mat4, vec3 } from "wgpu-matrix";

import {
  cubeVertexArray,
  cubeVertexSize,
  cubeUVOffset,
  cubePositionOffset,
  cubeVertexCount,
} from "./meshes/cube";

import basicVertWGSL from "./shaders/basic.vert.wgsl";
import vertexPositionColorWGSL from "./shaders/vertexPositionColor.frag.wgsl";
import { Subject, animationFrameScheduler, observeOn } from "rxjs";
import { SignalRConnection } from "../connection/signar-hubconnection";
import { RenderServiceLegacy } from "../render/RenderService";
import * as signalR from "@microsoft/signalr";

function createCanvasWithContext(
  device: GPUDevice,
  { width, height }: { width: number; height: number }
) {
  const presentationFormat = navigator.gpu.getPreferredCanvasFormat();
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext("webgpu") as GPUCanvasContext;
  context.configure({
    device,
    format: presentationFormat,
    alphaMode: "premultiplied",
  });
  const subject = new signalR.Subject<{
    Type: string;
    ClientX: number;
    ClientY: number;
    ClientWidth: number;
    ClientHeight: number;
  }>();
  SignalRConnection.send("MouseEvent", subject);
  canvas.addEventListener("mousemove", (e) => {
    const rect = canvas.getBoundingClientRect();
    subject.next({
      Type: e.type,
      ClientX: e.clientX,
      ClientY: e.clientY,
      ClientWidth: rect.width,
      ClientHeight: rect.height,
    });
  });
  canvas.addEventListener("mousedown", (e) => {
    const rect = canvas.getBoundingClientRect();
    subject.next({
      Type: e.type,
      ClientX: e.clientX,
      ClientY: e.clientY,
      ClientWidth: rect.width,
      ClientHeight: rect.height,
    });
  });
  canvas.addEventListener("mouseup", (e) => {
    const rect = canvas.getBoundingClientRect();
    subject.next({
      Type: e.type,
      ClientX: e.clientX,
      ClientY: e.clientY,
      ClientWidth: rect.width,
      ClientHeight: rect.height,
    });
  });
  return {
    canvas,
    context,
  };
}

export async function createWebGPURenderService(): Promise<RenderServiceLegacy> {
  const adapter = await navigator.gpu.requestAdapter();
  const device = await adapter?.requestDevice();
  if (!device) {
    throw new Error(`Failed to request adapter`);
  }
  const presentationFormat = navigator.gpu.getPreferredCanvasFormat();

  // const devicePixelRatio = window.devicePixelRatio;
  const { canvas, context } = createCanvasWithContext(device, {
    width: 800,
    height: 600,
  });

  // Create a vertex buffer from the cube data.
  const verticesBuffer = device.createBuffer({
    size: cubeVertexArray.byteLength,
    usage: GPUBufferUsage.VERTEX,
    mappedAtCreation: true,
  });
  new Float32Array(verticesBuffer.getMappedRange()).set(cubeVertexArray);
  verticesBuffer.unmap();

  const pipeline = device.createRenderPipeline({
    layout: "auto",
    vertex: {
      module: device.createShaderModule({
        code: basicVertWGSL,
      }),
      buffers: [
        {
          arrayStride: cubeVertexSize,
          attributes: [
            {
              // position
              shaderLocation: 0,
              offset: cubePositionOffset,
              format: "float32x4",
            },
            {
              // uv
              shaderLocation: 1,
              offset: cubeUVOffset,
              format: "float32x2",
            },
          ],
        },
      ],
    },
    fragment: {
      module: device.createShaderModule({
        code: vertexPositionColorWGSL,
      }),
      targets: [
        {
          format: presentationFormat,
        },
      ],
    },
    primitive: {
      topology: "triangle-list",

      // Backface culling since the cube is solid piece of geometry.
      // Faces pointing away from the camera will be occluded by faces
      // pointing toward the camera.
      cullMode: "back",
    },

    // Enable depth testing so that the fragment closest to the camera
    // is rendered in front.
    depthStencil: {
      depthWriteEnabled: true,
      depthCompare: "less",
      format: "depth24plus",
    },
  });

  const depthTexture = device.createTexture({
    size: [canvas.width, canvas.height],
    format: "depth24plus",
    usage: GPUTextureUsage.RENDER_ATTACHMENT,
  });

  const uniformBufferSize = 4 * 16; // 4x4 matrix
  const uniformBuffer = device.createBuffer({
    size: uniformBufferSize,
    usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
  });

  const uniformBindGroup = device.createBindGroup({
    layout: pipeline.getBindGroupLayout(0),
    entries: [
      {
        binding: 0,
        resource: {
          buffer: uniformBuffer,
        },
      },
    ],
  });

  const frame = (frame: number, mvp: Float32Array) => {
    device.queue.writeBuffer(
      uniformBuffer,
      0,
      mvp.buffer,
      mvp.byteOffset,
      mvp.byteLength
    );
    const renderPassDescriptor: GPURenderPassDescriptor = {
      colorAttachments: [
        {
          view: context.getCurrentTexture().createView(),

          clearValue: [0.5, 0.5, 0.5, 1.0],
          loadOp: "clear",
          storeOp: "store",
        },
      ],
      depthStencilAttachment: {
        view: depthTexture.createView(),

        depthClearValue: 1.0,
        depthLoadOp: "clear",
        depthStoreOp: "store",
      },
    };
    const commandEncoder = device.createCommandEncoder();
    const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);
    passEncoder.setPipeline(pipeline);
    passEncoder.setBindGroup(0, uniformBindGroup);
    passEncoder.setVertexBuffer(0, verticesBuffer);
    passEncoder.draw(cubeVertexCount);
    passEncoder.end();
    device.queue.submit([commandEncoder.finish()]);
  };

  const renderStates = new Subject<{
    time: number;
    state: number[];
  }>();
  const subscription =
    SignalRConnection.stream("RenderStates").subscribe(renderStates);

  renderStates.pipe(observeOn(animationFrameScheduler)).subscribe({
    next: ({ time, state }) => {
      frame(time, new Float32Array(state));
    },
    error: (e) => {
      console.error(e);
    },
  });

  return {
    canvas,
    dispose: () => {
      subscription.dispose();
      renderStates.complete();
    },
    attachToElement: (element) => {
      element.appendChild(canvas);
    },
  };
}
