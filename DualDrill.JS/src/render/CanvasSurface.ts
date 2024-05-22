// Abstraction of use canvas as a surface for rendering.
// WebGPU abstract away surface related details,
// We added this abstraction back for offscreen and distributed rendering scenarios.
// By window we added user input events subscription
export interface CanvasSurfaceWindow {
  readonly canvas: HTMLCanvasElement;
//   attachToElement(element: HTMLElement): void;
//   dispose(): void;
  showImage(image: GPUImageCopyExternalImageSource): void;
}

export function createCanvasSurfaceWindow(
  device: GPUDevice
): CanvasSurfaceWindow {
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("webgpu");
  if (!context) {
    throw new Error(`Failed to create webgpu context`);
  }
  return {
    canvas,
    showImage: (image) => {
      const presentTexture = context.getCurrentTexture();
      device.queue.copyExternalImageToTexture(
        {
          source: image,
        },
        {
          texture: presentTexture,
        },
        {
          width: presentTexture.width,
          height: presentTexture.height,
          depthOrArrayLayers: 1,
        }
      );
    },
  };
}
