import {
  filter,
  firstValueFrom,
  fromEvent,
  fromEventPattern,
  map,
  Observable,
} from "rxjs";
import { DualDrillEvent, Tags } from "./taggedEvent";
import { SignalConnection } from "./connection/server-signal-connection";
import {
  createPeerConnection,
  DualDrillConnection,
} from "./connection/dual-drill-connection";

interface WebView2SharedBufferMessage {
  getBuffer(): ArrayBuffer;
  additionalData: {
    SurfaceId: string;
  };
}

interface WebView2API {
  addEventListener(
    name: "sharedbufferreceived",
    handler: (e: WebView2SharedBufferMessage) => void
  ): void;
  removeEventListener(
    name: "sharedbufferreceived",
    handler: (e: WebView2SharedBufferMessage) => void
  ): void;
  addEventListener(
    name: "message",
    handler: (e: { data: string }) => void
  ): void;
  removeEventListener(
    name: "message",
    handler: (e: { data: string }) => void
  ): void;
  postMessage<T>(data: T): void;
}

function getWebView2(): WebView2API {
  return (window as any).chrome.webview;
}

function onWebViewSharedBufferMessage() {
  return fromEvent<WebView2SharedBufferMessage>(
    getWebView2(),
    "sharedbufferreceived"
  );
}

export function onWebviewMessage() {
  const webview = getWebView2();
  return fromEventPattern<WebViewHostToClientEvent>(
    (h) => {
      const handler = (e: { data: string }) => {
        h(JSON.parse(e.data));
      };
      webview.addEventListener("message", handler);
      return;
    },
    (_, h) => {
      webview.removeEventListener("message", h);
    }
  );
}

function WebViewSendMessageToHost<T>(tag: string, data: T) {
  const webview = getWebView2();
  webview.postMessage({
    type: tag,
    data,
  });
}

type WebViewHostToClientEvent =
  | DualDrillEvent<typeof Tags.OfferEvent>
  | DualDrillEvent<typeof Tags.AnswerEvent>
  | DualDrillEvent<typeof Tags.AddIceCandidateEvent>
  | DualDrillEvent<typeof Tags.BufferToPresent>
  | DualDrillEvent<typeof Tags.RequestPeerConnectionEvent>;

type WebViewClientToHostEvent = DualDrillEvent<typeof Tags.PointerEvent>;

interface SurfaceSharedBuffer {
  surfaceId: string;
  width: number;
  height: number;
  buffer: ArrayBuffer;
}

export class WebView2Service {
  readonly SurfaceSharedBuffers = new Map<string, SurfaceSharedBuffer>();
  readonly PeerConnections = new Map<string, DualDrillConnection>();
  MainSurfaceId?: string;
  MainStream?: MediaStream;

  ReceivedStream?: MediaStream;

  constructor(private readonly BaseUrl = "/api/webviewInterop") {
    this.onEvent(Tags.RequestPeerConnectionEvent).subscribe({
      next: async ({ sourceId, targetId }) => {
        if (this.PeerConnections.has(sourceId)) {
          throw new Error(`PeerConnection ${sourceId} already exists`);
        }
        console.log(
          `start create peer connection for ${sourceId} to ${targetId}`
        );
        const signalConnection = this.SignalConnection(sourceId);
        const pc = createPeerConnection(signalConnection, true);
        pc.onTrack.subscribe(async (t) => {
          console.log(`received track from ${sourceId} with id ${t.track.id}`);
          if (t.streams[0]) {
            this.ReceivedStream = t.streams[0];
          } else {
            this.ReceivedStream = new MediaStream([
              t.transceiver.receiver.track,
            ]);
          }
        });
        this.PeerConnections.set(sourceId, pc);
        console.log(`Create Peer Connection for client id ${sourceId}`);
        if (this.MainStream) {
          console.log(`adding track for stream ${this.MainStream.id}`);
          const track = this.MainStream.getVideoTracks()[0];
          track.contentHint = "detail";
          console.log(track);
          const sender = pc.addTrack(track, this.MainStream);
          // const parameters = sender.getParameters();
          // parameters.encodings[0].maxBitrate = 5000000; // Set the maximum bitrate to 5 Mbps
          // parameters.encodings[0].scaleResolutionDownBy = 1.0; // Do not scale down the resolution
          // parameters.encodings[0].maxFramerate = 30; // Set the maximum framerate to 30 fps
          // parameters.encodings[0].priority = "high";
          // parameters.encodings[0].networkPriority = "high";
          // console.log(parameters);
          // await sender.setParameters(parameters);
        } else {
          console.warn("track is not ready yet");
        }
        console.log("start connection");
        // await pc.start();
      },
    });
  }

  public async getSurfaceSharedBuffer(surfaceId?: string) {
    // TODO: using for subscription release
    // TODO: error handling

    const bufferPromise = firstValueFrom(
      onWebViewSharedBufferMessage().pipe(map((e) => e.getBuffer()))
    );

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

  public onEvent<Tag extends WebViewHostToClientEvent["type"]>(
    tag: Tag
  ): Observable<DualDrillEvent<Tag>["data"]> {
    console.log(tag);
    return onWebviewMessage().pipe(
      filter((m) => m.type === tag),
      map((e) => e.data)
    ) as Observable<DualDrillEvent<Tag>["data"]>;
  }

  public send<Tag extends WebViewClientToHostEvent["type"]>(
    tag: Tag,
    data: DualDrillEvent<Tag>["data"]
  ) {
    WebViewSendMessageToHost(tag, data);
  }

  public drawSurface(surfaceId: string) {
    const surface = this.SurfaceSharedBuffers.get(surfaceId);
    if (!surface) {
      throw new Error(`Surface #${surfaceId} not found`);
    }
    const canvas = document.createElement("canvas");
    canvas.width = surface.width;
    canvas.height = surface.height;
    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error(`Failed to get 2d context`);
    }

    onWebviewMessage()
      .pipe(
        filter((m) => m.type === Tags.BufferToPresent),
        map((m) => m.data)
      )
      .subscribe({
        next: (msg) => {
          const data = new Uint8ClampedArray(
            surface.buffer,
            msg.offset,
            msg.length
          );
          const imageData = new ImageData(data, msg.width, msg.height);
          context.putImageData(imageData, 0, 0);
        },
      });

    return canvas;
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

  private SignalConnection(clientId: string): SignalConnection {
    const baseUrl = "/api/SignalConnection";
    const serverId = "00000000-0000-0000-0000-000000000000";
    return {
      id: clientId,
      offer: async (sdp) => {
        await fetch(`${baseUrl}/Offer/${serverId}/${clientId}`, {
          method: "POST",
          headers: {
            "Content-Type": "text/plain",
          },
          body: sdp,
        });
      },
      onOffer: this.onEvent(Tags.OfferEvent).pipe(
        filter((m) => m.sourceId === clientId),
        map((m) => m.data.sdp)
      ),
      answer: async (sdp) => {
        await fetch(`${baseUrl}/Answer/${serverId}/${clientId}`, {
          method: "POST",
          headers: {
            "Content-Type": "text/plain",
          },
          body: sdp,
        });
      },
      onAnswer: this.onEvent(Tags.AnswerEvent).pipe(
        filter((m) => m.sourceId === clientId),
        map((m) => m.data.sdp)
      ),
      addIceCandidate: async (candidate) => {
        await fetch(`${baseUrl}/AddIceCandidate/${serverId}/${clientId}`, {
          method: "POST",
          headers: {
            "Content-Type": "text/plain",
          },
          body: JSON.stringify(candidate),
        });
      },
      onAddIceCandidate: this.onEvent(Tags.AddIceCandidateEvent).pipe(
        filter((m) => m.sourceId === clientId),
        map((m) => JSON.parse(m.data.candidate))
      ),
    };
  }
}
