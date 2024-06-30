import { BlazorServerService } from "../client";
import { SignalRConnection } from "../lib/signalr-client";
import { RenderServiceLegacy } from "./RenderService";
import * as signalR from "@microsoft/signalr";

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
  const width = 1472;
  const height = 936 * 2;

  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext("2d");
  if (!context) {
    throw new Error("Failed to create 2d context");
  }
  const sharedBuffer = await new Promise<ArrayBuffer>((resolve, reject) => {
    const h = async (e: WebviewSharedBufferMessage) => {
      const buf = e.getBuffer();
      console.log(buf);
      if (BlazorServerService !== null) {
        console.log(BlazorServerService);
        await BlazorServerService.invokeMethodAsync(
          "OnBuffer",
          DotNet.createJSObjectReference(buf)
        );
      }
      resolve(buf);
      (window as any).chrome.webview.removeEventListener(
        "sharedbufferreceived",
        h
      );
    };
    (window as any).chrome.webview.addEventListener("sharedbufferreceived", h);
  });
  console.log(`shared buffer received (length = ${sharedBuffer.byteLength})`);

  const frameBuffers: SharedBufferMessage[] = [];
  const generatedBuffersStreamSubscription =
    SignalRConnection.stream<SharedBufferMessage>(
      "SharedBufferServerRenderingReadable"
    ).subscribe({
      next: (e) => {
        frameBuffers.push(e);
      },
      complete: () => {
        console.log("read channel complete");
      },
      error: (e) => {
        console.error(e);
      },
    });
  const consumedBuffers = new signalR.Subject<SharedBufferMessage>();
  SignalRConnection.send(
    "SharedBufferServerRenderingWriteable",
    consumedBuffers
  );

  let requestAnimationFrameHandle: number = 0;

  const fpsDiv = document.createElement("span");
  const FPSSampleFrameCount = 100;
  let lastPerfNow = performance.now();
  function render(frame: number, skipped: number) {
    const frameBuffer = frameBuffers.shift()!;
    if (frameBuffer !== undefined) {
      const data = new Uint8Array(
        sharedBuffer,
        frameBuffer.offset,
        frameBuffer.length
      );
      const image = new ImageData(new Uint8ClampedArray(data), width, height);
      context?.putImageData(image as ImageData, 0, 0);
      consumedBuffers.next(frameBuffer);
    } else {
      skipped += 1;
    }
    if (frame > 0 && frame % FPSSampleFrameCount === 0) {
      const currNow = performance.now();
      const elapsed = currNow - lastPerfNow;
      lastPerfNow = currNow;
      const msInSec = 1000;
      const message = `FPS ${msInSec / (elapsed / FPSSampleFrameCount)}(frame: ${frame}, skipped: ${skipped}(${(skipped / frame) * 100}%))`;
      fpsDiv.innerText = message;
    }
    requestAnimationFrameHandle = requestAnimationFrame(() =>
      render(frame + 1, skipped)
    );
  }
  requestAnimationFrameHandle = requestAnimationFrame(() => {
    render(0, 0);
  });
  return {
    canvas,
    attachToElement(el) {
      el.appendChild(fpsDiv);
      el.appendChild(canvas);
    },
    dispose() {
      cancelAnimationFrame(requestAnimationFrameHandle);
      consumedBuffers.complete();
      generatedBuffersStreamSubscription.dispose();
    },
  };
}
