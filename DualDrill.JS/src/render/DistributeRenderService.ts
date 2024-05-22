export interface ServerRenderPresentService {
  attachToElement: (element: HTMLElement) => void;
  createServerRenderTextureSwapChain(
    swapChainSize: number,
    targetContext: GPUCanvasContext
  ): ServerRenderCanvasTextureSwapChain;
  dispose: () => void;
}

export interface ServerRenderCanvasTextureSwapChain {
  readonly id: string;
  readonly swapChainSize: number;
  acquireTexture(): Promise<{ index: number; width: number; height: number }>;
  updateTexture(index: number, buffer: Blob): void;
  present(): void;
}

export async function createServerRenderPresentService(): Promise<ServerRenderPresentService> {
  const width = 800;
  const height = 600;
  const adapter = await navigator.gpu.requestAdapter();
  const device = await adapter?.requestDevice();
  if (!device) {
    throw new Error(`Failed to request adapter`);
  }
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  // const context = canvas.getContext("webgpu");
  // if (!context) {
  //   throw new Error(`Failed to get webgpu context`);
  // }
  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error(`Failed to get 2d context`);
  }

  async function testFetchRenderResult() {
    const res = await fetch("/api/vulkan/render");
    const b = await res.blob();

    const imageData = new ImageData(width, height);
    imageData.data.set(new Uint8ClampedArray(await b.arrayBuffer()));
    context?.putImageData(imageData, 0, 0);
    // device?.queue.copyExternalImageToTexture(
    //   {
    //     source: imageData,
    //   },
    //   {
    //     texture: context!.getCurrentTexture(),
    //   },
    //   {
    //     width,
    //     height,
    //     depthOrArrayLayers: 1,
    //   }
    // );

    requestAnimationFrame(testFetchRenderResult);
  }

  requestAnimationFrame(testFetchRenderResult);
  // context.configure({
  //   device,
  //   format: navigator.gpu.getPreferredCanvasFormat(),
  // });

  return {
    attachToElement: (element) => {
      element.appendChild(canvas);
    },
    dispose: () => {
      console.error("not implemented yet");
    },
    createServerRenderTextureSwapChain(
      swapChainSize: number,
      targetContext: GPUCanvasContext
    ): ServerRenderCanvasTextureSwapChain {
      const id = crypto.randomUUID();
      const textures = Array.from({ length: swapChainSize }, () => {
        const currentTexture = targetContext.getCurrentTexture();
        device.createTexture({
          dimension: "2d",
          format: "rgba8uint",
          size: {
            width: currentTexture.width,
            height: currentTexture.height,
            depthOrArrayLayers: 1,
          },
          usage: GPUTextureUsage.COPY_DST | GPUTextureUsage.COPY_SRC,
        });
      });
      throw new Error(`Not implemented`);
    },
  };
}
