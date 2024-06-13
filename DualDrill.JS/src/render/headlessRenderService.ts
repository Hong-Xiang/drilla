import { RenderServiceLegacy } from "./RenderService";

// fetch image from backend with /api/wgpu/render?time=100 url and render it with given canvas' 2d context
async function fetchAndDrawImage(
  context: CanvasRenderingContext2D,
  time: number
) {
  const image = await fetch(`api/wgpu/render?time=${time}`);
  const blob = await image.blob();
  const url = URL.createObjectURL(blob);
  const img = new Image();
  img.src = url;
  img.onload = () => {
    context.drawImage(img, 0, 0);
  };
}

export async function createHeadlessServerRenderService(): Promise<RenderServiceLegacy> {
  const canvas = document.createElement("canvas");
  canvas.width = 512;
  canvas.height = 512;

  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Failed to create context");
  }

  await fetchAndDrawImage(context, 1000);

  let frame = 0;
  let h: number = 0;
  const buffer: (HTMLImageElement | "fetching" | "idle")[] = [
    "idle",
    "idle",
    "idle",
    "idle",
    "idle",
    "idle",
    "idle",
    "idle",
  ];

  const fpsDiv = document.createElement("span");
  let rendered = 0;
  let lastPerfNow = performance.now();
  let frameDrop = 0;
  let fetchDrop = 0;
  function render(
    frame: number,
    fetchBufferIndex: number,
    consumeBufferIndex: number
  ) {
    const time = (frame * 1000) % 200000;
    const fetched = buffer[consumeBufferIndex] instanceof HTMLImageElement;
    if (!fetched) {
      frameDrop += 1;
    }
    const needFetch = buffer[fetchBufferIndex] === "idle";
    if (!needFetch) {
      fetchDrop += 1;
    }
    if (needFetch) {
      buffer[fetchBufferIndex] = "fetching";
      fetch(`api/wgpu/render?time=${time}`).then(async (image) => {
        const blob = await image.blob();
        const url = URL.createObjectURL(blob);
        const img = new Image();
        img.src = url;
        img.onload = () => {
          buffer[fetchBufferIndex] = img;
        };
      });
    }
    if (fetched) {
      if (rendered % 100 === 0) {
        const currNow = performance.now();
        const elapsed = currNow - lastPerfNow;
        lastPerfNow = currNow;
        const message = `rendered ${rendered} @ FPS ${1000 / (elapsed / 100)}(missRender: ${frameDrop}, missFetch: ${fetchDrop})`;
        fpsDiv.innerText = message;
      }
      rendered++;
      const image = buffer[consumeBufferIndex]!;
      context?.drawImage(image as HTMLImageElement, 0, 0);
      buffer[consumeBufferIndex] = "idle";
    }
    // await new Promise<number>((resolve, reject) => {
    //   setTimeout(() => {
    //     resolve(0);
    //   }, 100);
    // });
    frame += 1;
    h = requestAnimationFrame((time) =>
      render(
        frame + 1,
        needFetch ? (fetchBufferIndex + 1) % buffer.length : fetchBufferIndex,
        fetched ? (consumeBufferIndex + 1) % buffer.length : consumeBufferIndex
      )
    );
  }

  h = requestAnimationFrame(() => {
    render(0, 0, 0);
  });

  return {
    canvas,
    attachToElement(el) {
      el.appendChild(fpsDiv);
      el.appendChild(canvas);
    },
    dispose() {
      cancelAnimationFrame(h);
    },
  };
}
