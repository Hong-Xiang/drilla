import shaderCode from "./webgpu/shaders/fullscreenTexturedQuad.wgsl";
import GUI from "lil-gui";

export async function VolumeRenderingMain() {
  console.log("volume rendering application start");
  const dataResponse = await fetch("/api/data/head");
  const data = await dataResponse.arrayBuffer();
  console.log(data.byteLength);
  const depth = data.byteLength / 256 / 256;
  //   const unzipData = await zip.loadAsync(data);
  //   console.log(unzipData.files);
  const adapter = await navigator.gpu.requestAdapter();
  const device = await adapter?.requestDevice();
  if (!device) {
    throw new Error(`Failed to request adapter`);
  }
  const canvas = document.getElementById("target-canvas") as
    | HTMLCanvasElement
    | undefined;
  if (!canvas || !(canvas instanceof HTMLCanvasElement)) {
    throw Error("Failed to get canvas");
  }
  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error(`Failed to get webgpu context`);
  }
  context.configure({
    device,
    format: "bgra8unorm",
    alphaMode: "opaque",
  });

  const quad = await createGPUMesh(device, "Quad");
  console.log(quad);

  const module = device.createShaderModule({
    code: shaderCode,
  });

  const pipeline = device.createRenderPipeline({
    layout: "auto",
    vertex: {
      module,
      buffers: [
        // {
        //   arrayStride: cubeVertexSize,
        //   attributes: [
        //     {
        //       // position
        //       shaderLocation: 0,
        //       offset: cubePositionOffset,
        //       format: "float32x4",
        //     },
        //     {
        //       // uv
        //       shaderLocation: 1,
        //       offset: cubeUVOffset,
        //       format: "float32x2",
        //     },
        //   ],
        // },
      ],
    },
    fragment: {
      module,
      targets: [
        {
          format: "bgra8unorm",
        },
      ],
    },
    primitive: {
      topology: "triangle-list",

      // Backface culling since the cube is solid piece of geometry.
      // Faces pointing away from the camera will be occluded by faces
      // pointing toward the camera.
      //   cullMode: "back",
    },

    // Enable depth testing so that the fragment closest to the camera
    // is rendered in front.
    // depthStencil: {
    //   depthWriteEnabled: true,
    //   depthCompare: 'never',
    //   format: "depth24plus",
    // },
  });

  const dataTexture = device.createTexture({
    dimension: "3d",
    size: [256, 256, 109],
    format: "r8unorm",
    usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
  });

  device.queue.writeTexture(
    { texture: dataTexture },
    data,
    {
      offset: 0,
      bytesPerRow: 256,
      rowsPerImage: 256,
    },
    {
      width: 256,
      height: 256,
      depthOrArrayLayers: depth,
    }
  );

  const depthTexture = device.createTexture({
    size: [canvas.width, canvas.height],
    format: "depth24plus",
    usage: GPUTextureUsage.RENDER_ATTACHMENT,
  });
  const depthView = depthTexture.createView();

  const uniformBufferSize = 4 * 16; // 4x4 matrix
  const uniformBuffer = device.createBuffer({
    size: uniformBufferSize,
    usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
  });
  const sampler = device.createSampler({
    addressModeU: "clamp-to-edge",
    addressModeV: "clamp-to-edge",
    addressModeW: "clamp-to-edge",
    magFilter: "linear",
    minFilter: "linear",
    mipmapFilter: "linear",
  });
  const layout = pipeline.getBindGroupLayout(0);
  console.log(layout);
  const bindGroup = device.createBindGroup({
    layout,
    entries: [
      {
        binding: 0,
        resource: sampler,
      },
      {
        binding: 1,
        resource: dataTexture.createView(),
      },
      {
        binding: 2,
        resource: {
          buffer: uniformBuffer,
          offset: 0,
          size: 4 * 4,
        },
      },
    ],
  });

  //   const uniformBindGroup = device.createBindGroup({
  //     layout: pipeline.getBindGroupLayout(0),
  //     entries: [
  //       {
  //         binding: 0,
  //         resource: {
  //           buffer: uniformBuffer,
  //         },
  //       },
  //     ],
  //   });

  const uniformBufferValue = new Float32Array(4);
  const gui = new GUI();
  const state = {
    theta: Math.PI / 2,
    phi: 0,
    z: 0,
    window: 0.1,
  };
  gui.add(state, "theta", 0, Math.PI);
  gui.add(state, "phi", 0, Math.PI * 2);
  gui.add(state, "z", -Math.sqrt(3), Math.sqrt(3));
  gui.add(state, "window", 0.01, Math.sqrt(3));

  const frame = (index: number) => {
    const period = 60 * 5;
    uniformBufferValue[0] = state.theta;
    uniformBufferValue[1] = state.phi;
    uniformBufferValue[2] = state.z;
    uniformBufferValue[3] = state.window;
    device.queue.writeBuffer(
      uniformBuffer,
      0,
      uniformBufferValue.buffer,
      0,
      uniformBufferValue.byteLength
    );
    const renderPassDescriptor: GPURenderPassDescriptor = {
      colorAttachments: [
        {
          view: context.getCurrentTexture().createView(),
          clearValue: [0.0, 0.0, 0.0, 1.0],
          loadOp: "clear",
          storeOp: "store",
        },
      ],
      //   depthStencilAttachment: {
      //     view: depthView,
      //     depthClearValue: 0.0,
      //     depthLoadOp: "clear",
      //     depthStoreOp: "store",
      //   },
    };
    const commandEncoder = device.createCommandEncoder();
    const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);
    passEncoder.setPipeline(pipeline);
    passEncoder.setBindGroup(0, bindGroup);
    // passEncoder.setVertexBuffer(0, quad.vertexBuffer);
    // passEncoder.setIndexBuffer(quad.indexBuffer, quad.meta.indexFormat);
    passEncoder.draw(6);
    passEncoder.end();
    device.queue.submit([commandEncoder.finish()]);
    requestAnimationFrame(() => {
      frame(index + 1);
    });
  };

  requestAnimationFrame(() => {
    frame(0);
  });
}

interface MeshMetaData {
  readonly name: string;
  readonly bufferLayout: GPUVertexBufferLayout;
  readonly indexCount: number;
  readonly indexFormat: GPUIndexFormat;
}

async function createGPUMesh(device: GPUDevice, name: string) {
  const baseUrl = `/api/mesh/${name}`;
  const meta = (await (await fetch(baseUrl + "/meta")).json()) as MeshMetaData;
  const vertexData = await (await fetch(baseUrl + "/vertex")).arrayBuffer();
  const indexData = await (await fetch(baseUrl + "/index")).arrayBuffer();
  const vertexBuffer = device.createBuffer({
    size: vertexData.byteLength,
    usage: GPUBufferUsage.VERTEX,
    mappedAtCreation: true,
  });
  new Uint8Array(vertexBuffer.getMappedRange()).set(new Uint8Array(vertexData));
  const indexBuffer = device.createBuffer({
    size: indexData.byteLength,
    usage: GPUBufferUsage.VERTEX,
    mappedAtCreation: true,
  });
  new Uint8Array(indexBuffer.getMappedRange()).set(new Uint8Array(indexData));

  return {
    meta,
    vertexBuffer,
    indexBuffer,
  };
}

function inputUI(name: string, value: number, min: number, max: number) {
  const inputEl = document.getElementById(`${name}-input`) as HTMLInputElement;
  inputEl.min = min.toString();
  inputEl.max = max.toString();
  inputEl.step = ((max - min) / 100).toString();
  const valueEl = document.getElementById(`${name}-value`) as HTMLSpanElement;

  const result = {
    value,
  };
  inputEl.value = value.toString();
  function update(v: number) {
    valueEl.innerText = v.toString();
    result.value = v;
  }
  update(value);
  inputEl.oninput = (e) => {
    const value = parseFloat(inputEl.value);
    update(value);
  };
  return result;
}
