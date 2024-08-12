import {
  onWebviewMessage,
  onWebViewSharedBufferMessage,
} from "./connection/webview2-connection";

interface SurfaceSharedBuffer {
  surfaceId: string;
  width: number;
  height: number;
  buffer: ArrayBuffer;
}

export class WebView2Service {
  readonly SurfaceSharedBuffers = new Map<string, SurfaceSharedBuffer>();
  readonly PeerConnections = new Map<string, RTCPeerConnection>();
  MainSurfaceId?: string;

  constructor(private readonly BaseUrl = "/api/webviewInterop") {}

  public async getSurfaceSharedBuffer(surfaceId?: string) {
    // TODO: using for subscription release
    // TODO: error handling
    const bufferPromise = new Promise<ArrayBuffer>((resolve, reject) => {
      const subscription = onWebViewSharedBufferMessage().subscribe((e) => {
        //   console.log(e);
        //   console.log(surfaceId);
        //   console.log(e.surfaceId === surfaceId);
        //   if (e.surfaceId === surfaceId) {
        resolve(e.getBuffer());
        subscription.unsubscribe();
        //   }
      });
    });
    let url = `${this.BaseUrl}/SurfaceSharedBuffer`;
    if (surfaceId) {
      url += `?surfaceId=${surfaceId}`;
    }
    const r = await fetch(url);
    if (!r.ok) {
      throw new Error(
        `Failed to get shared buffer for surface ${surfaceId}: ${r.statusText}`
      );
    }
    const data = await r.json();
    const buffer = await bufferPromise;
    const surfaceSharedBuffer: SurfaceSharedBuffer = {
      surfaceId: data.id,
      width: data.option.width,
      height: data.option.height,
      buffer,
    };
    this.SurfaceSharedBuffers.set(
      surfaceSharedBuffer.surfaceId,
      surfaceSharedBuffer
    );

    if (!surfaceId) {
      this.MainSurfaceId = surfaceSharedBuffer.surfaceId;
    }

    console.log("surface shared buffer created", surfaceSharedBuffer);
    return surfaceSharedBuffer;
  }

  public drawSurface(surfaceId: string, context: CanvasRenderingContext2D) {
    const surfaceSharedBuffer = this.SurfaceSharedBuffers.get(surfaceId);
    if (!surfaceSharedBuffer) {
      throw new Error(`Surface shared buffer not found: ${surfaceId}`);
    }
    onWebviewMessage().subscribe({
      next: (data) => {
        const msg = JSON.parse(data);
        console.log(msg);
        if (msg.MessageType === "BufferToPresentEvent") {
          const imageData = new ImageData(
            new Uint8ClampedArray(
              surfaceSharedBuffer.buffer,
              msg.Offset,
              msg.Length
            ),
            msg.Width,
            msg.Height
          );
          context.putImageData(imageData, 0, 0);
        }
      },
    });
  }

  public async capturedStream(surfaceId: string, streamId: string) {
    const r = await fetch(
      `${this.BaseUrl}/capturedStream/${surfaceId}/${streamId}`,
      {
        method: "POST",
      }
    );
    if (!r.ok) {
      throw new Error(
        `Failed to report captured stream surfaceId ${surfaceId}, streamId: ${streamId}: ${r.statusText}`
      );
    }
  }
}
