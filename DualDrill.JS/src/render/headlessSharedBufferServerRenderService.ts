import { SignalRConnection } from "../lib/signalr-client";
import { RenderServiceLegacy } from "./RenderService";
import * as signalR from "@microsoft/signalr";

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

interface SharedBufferMessage {
  offset: number;
  length: number;
  slotIndex: number;
}

interface WebviewSharedBufferMessage {
  getBuffer(): ArrayBuffer;
}

export async function createHeadlessSharedBufferServerRenderService(): Promise<RenderServiceLegacy> {
  const canvas = document.createElement("canvas");
  // const width = 512;
  // const height = 512;
  const slotCount = 3;
  const width = 1472;
  const height = 936 * 2;

  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Failed to create context");
  }
  // (window as any).chrome.webview.addEventListener("message", (e: any) => {
  //   console.log(e);
  // });
  console.log((window as any).chrome.webview);
  // (window as any).chrome.webview.addEventListener(
  //   "sharedbufferreceived",
  //   (e: any) => {
  //     console.log(e);
  //   }
  // );
  console.log("add shared buffer listener");
  const sharedBuffer = await new Promise<ArrayBuffer>((resolve, reject) => {
    const h = (e: WebviewSharedBufferMessage) => {
      // console.log(e);
      resolve(e.getBuffer());
      (window as any).chrome.webview.removeEventListener(
        "sharedbufferreceived",
        h
      );
    };
    (window as any).chrome.webview.addEventListener("sharedbufferreceived", h);
  });
  console.log(sharedBuffer);
  console.log(sharedBuffer.byteLength);
  const buffers: SharedBufferMessage[] = [];
  SignalRConnection.stream<SharedBufferMessage>(
    "SharedBufferServerRenderingReadable"
  ).subscribe({
    next: (e) => {
      console.log(e);
      buffers.push(e);
      console.log(buffers);
    },
    complete: () => {
      console.log("read channel complete");
    },
    error: (e) => {
      console.error(e);
    },
  });
  const readed = new signalR.Subject<SharedBufferMessage>();
  SignalRConnection.send("SharedBufferServerRenderingWriteable", readed);

  let frame = 0;
  let h: number = 0;

  const fpsDiv = document.createElement("span");
  let rendered = 0;
  let lastPerfNow = performance.now();
  let frameDrop = 0;
  let fetchDrop = 0;
  function render(frame: number, readIndex: number) {
    const time = (frame * 1000) % 200000;
    const fetched = !!buffers;
    console.log(`fetched ${buffers}`);
    if (!fetched) {
      frameDrop += 1;
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
      console.log(buffers);
      const bufferMessage = buffers.shift()!;
      if (bufferMessage) {
        console.log(bufferMessage);
        const data = new Uint8Array(
          sharedBuffer,
          bufferMessage.offset,
          bufferMessage.length
        );
        // console.log(data);
        const image = new ImageData(new Uint8ClampedArray(data), width, height);
        context?.putImageData(image as ImageData, 0, 0);
        readed.next(bufferMessage);
      }
      // bufferStates[readIndex] = "writeable";
    }
    // await new Promise<number>((resolve, reject) => {
    //   setTimeout(() => {
    //     resolve(0);
    //   }, 100);
    // });
    frame += 1;
    h = requestAnimationFrame((time) =>
      render(frame + 1, fetched ? (readIndex + 1) % buffers.length : readIndex)
    );
  }

  h = requestAnimationFrame(() => {
    render(0, 0);
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
