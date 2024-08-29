import { filter, first, fromEvent, map } from "rxjs";
import { DotNetObject } from "./blazor-dotnet";
import {
  PromiseLikeResultMapper,
  subscribeByPromiseLike,
} from "./lib/dotnet-server-interop";
import { SignalRConnection } from "./connection/signar-hubconnection";
export {
  StartSignalRHubConnection,
  SignalRConnection,
} from "./connection/signar-hubconnection";

export { getProperty, setProperty } from "./lib/dotnet-server-interop";
export { createWebGPURenderService } from "./webgpu/rotateCube";
export { createServerRenderPresentService } from "./render/DistributeRenderService";
export { createHeadlessSharedBufferServerRenderService } from "./render/headlessSharedBufferServerRenderService";
export { createHeadlessServerRenderService } from "./render/headlessRenderService";
export { getDotnetWasmExports } from "./lib/jsexport-client";
export { createAsyncMessageEmitter } from "./asyncMessage";
import { getDotnetWasmExports } from "./lib/jsexport-client";
import { createSignalConnectionOverSignalRHubConnection } from "./connection/signalr-server-connection";
import {
  createPeerConnection,
  DualDrillConnection,
} from "./connection/dual-drill-connection";
import { onWebviewMessage, WebView2Service } from "./webview2service";
import {
  createSignalConnectionOverWebSocket,
  startWebsocketConnection,
} from "./connection/websocket";
import { Tags } from "./taggedEvent";
export { startWebsocketConnection } from "./connection/websocket";
export { VolumeRenderingMain } from "./volume-rendering";

export async function testDotnetExport() {
  const exports = await getDotnetWasmExports("DualDrill.Client.dll");
}

export function asObjectReference<T>(x: T) {
  return x;
}

export function newOperator(cls: any, ...args: unknown[]) {
  return new cls(...args);
}

export function callFunction(f: any, ...args: unknown[]) {
  return f(...args);
}

export function unpackCallback(f: () => unknown) {
  return f();
}

export function getGlobalThis() {
  return globalThis;
}

export let BlazorServerService: DotNetObject | null = null;

export function SignalRConnectionId() {
  return SignalRConnection.connectionId;
}

export function createCanvas(
  pointerDown: DotNetObject,
  pointerMove: DotNetObject,
  pointerUp: DotNetObject
) {
  const canvas = new HTMLCanvasElement();
  const normalizePosition = (e: PointerEvent) => {
    const target = e.target as HTMLCanvasElement;
    return {
      OffsetX: e.offsetX,
      OffsetY: e.offsetY,
      Width: target.offsetWidth,
      Height: target.offsetHeight,
    };
  };
  const subscription = fromEvent<PointerEvent>(canvas, "pointerdown")
    .pipe(map(normalizePosition))
    .subscribe({
      next: (e) => pointerDown.invokeMethodAsync("OnNext", e),
    });

  subscription.add(
    fromEvent<PointerEvent>(canvas, "pointermove")
      .pipe(map(normalizePosition))
      .subscribe({
        next: (e) => pointerMove.invokeMethodAsync("OnNext", e),
      })
  );
  subscription.add(
    fromEvent<PointerEvent>(canvas, "pointerup")
      .pipe(map(normalizePosition))
      .subscribe({
        next: (e) => pointerUp.invokeMethodAsync("OnNext", e),
      })
  );

  return {
    dispose: () => {
      subscription.unsubscribe();
    },
  };
}

export function getElementSize(element: HTMLElement) {
  return {
    OffsetWidth: element.offsetWidth,
    OffsetHeight: element.offsetHeight,
  };
}

export function captureStream(canvas: HTMLCanvasElement): MediaStream {
  const stream = canvas.captureStream(30);
  return stream;
}

export function createRTCPeerConnection() {
  const connection = new RTCPeerConnection();

  connection.addEventListener("negotiationneeded", (e) => console.log(e));
  connection.addEventListener("track", (e) => console.log(e));

  function addEventObserver<
    K extends keyof RTCPeerConnectionEventMap,
    T = RTCPeerConnectionEventMap[K],
  >(
    dotnetSubject: DotNetObject,
    type: K,
    transform: (e: RTCPeerConnectionEventMap[K]) => T | null
  ) {
    const h = async (e: RTCPeerConnectionEventMap[K]) => {
      const v = transform(e);
      if (v === null) {
        return;
      }
      await dotnetSubject.invokeMethodAsync("OnNext", v);
    };
    connection.addEventListener(type, h);
    return {
      dispose: () => connection.removeEventListener(type, h),
    };
  }

  return {
    createDataChannel: async (label: string) => {
      const channel = connection.createDataChannel(label);
      return channel;
    },
    async waitDataChannel(label: string, tcs: DotNetObject) {
      let found = false;
      let disposed = false;
      const h = async (e: RTCDataChannelEvent) => {
        if (!found && e.channel.label === label) {
          found = true;
          disposed = true;
          await tcs.invokeMethodAsync(
            "SetResult",
            DotNet.createJSObjectReference(e.channel)
          );
        }
      };
      connection.addEventListener("datachannel", h);
      return {
        dispose: () => {
          if (!disposed) {
            connection.removeEventListener("datachannel", h);
          }
        },
      };
    },
    addVideoStream: async (stream: MediaStream) => {
      console.log(`add video stream ${stream.id}`);
      const tracks = stream.getVideoTracks();
      if (tracks.length !== 1) {
        throw new Error(
          `Currently only support 1 video track, got ${tracks.length}`
        );
      }
      connection.addTrack(tracks[0], stream);
    },
    async waitVideoStream(streamId: string, pb: DotNetObject) {
      const r = fromEvent<RTCTrackEvent>(connection, "track").pipe(
        filter((e) => e.streams.length === 1 && e.streams[0].id === streamId),
        first(),
        map((e) => e.streams[0])
      );
      return subscribeByPromiseLike(
        r,
        pb,
        PromiseLikeResultMapper.JSObjectReferences
      );
    },
    subscribe<K extends keyof RTCPeerConnectionEventMap>(
      dotnetSubject: DotNetObject,
      type: K
    ) {
      switch (type) {
        case "connectionstatechange":
          return addEventObserver(
            dotnetSubject,
            "connectionstatechange",
            () => connection.connectionState
          );
        case "icecandidate":
          return addEventObserver(dotnetSubject, "icecandidate", (e) => {
            if (e.candidate) {
              return e.candidate;
            } else {
              return null;
            }
          });
        case "negotiationneeded":
          return addEventObserver(dotnetSubject, "negotiationneeded", () => 0);
        case "track":
          return addEventObserver(dotnetSubject, "track", (e) => {
            if (e.streams.length === 1) {
              return e.streams[0];
            } else {
              console.warn(
                `Currently only support track with 1 stream, got ${e.streams.length}`
              );
              return null;
            }
          });
        default:
          throw new Error(`Not supported type ${type}`);
      }
    },
    getConnectionState: () => connection.connectionState,
    addIceCandidate: async (candidate: RTCIceCandidateInit) => {
      await connection.addIceCandidate(candidate);
    },
    createOffer: async () => {
      const offer = await connection.createOffer({
        offerToReceiveVideo: true,
      });
      console.log("create offer");
      console.log(offer.sdp);
      return offer.sdp;
    },
    async setRemoteDescription(type: "offer" | "answer", sdp: string) {
      await connection.setRemoteDescription({
        type,
        sdp,
      });
    },
    async setLocalDescription(type: "offer" | "answer", sdp: string) {
      await connection.setLocalDescription({
        type,
        sdp,
      });
    },
    createAnswer: async () => {
      const answer = await connection.createAnswer();
      console.log("return answer");
      console.log(answer.sdp);
      return answer.sdp;
    },
    dispose: () => {
      connection.close();
    },
  };
}

export function addDataChannelLogMessageListener(channel: RTCDataChannel) {
  const h = (e: MessageEvent<unknown>) => {
    console.log(e.data);
  };
  channel.addEventListener("message", h);
  return {
    dispose: () => {
      channel.removeEventListener("message", h);
    },
  };
}

function normalizedPointerEvent(e: PointerEvent, t: HTMLElement) {
  const rect = t.getBoundingClientRect();
  const x = (e.offsetX / rect.width) * 2 - 1;
  const y = -((e.offsetY / rect.height) * 2 - 1);
  const result = {
    x,
    y,
    surfaceWidth: rect.width,
    surfaceHeight: rect.height,
  };
  return result;
}

export async function CreateSimpleRTCClient(clientId: string) {
  const video = document.createElement("video");
  video.autoplay = true;
  video.playsInline = true;
  video.muted = true;

  console.log("create simple RTC client");

  const serverId = "00000000-0000-0000-0000-000000000000";

  const signalServer = createSignalConnectionOverSignalRHubConnection(
    SignalRConnection,
    serverId
  );
  const connection = createPeerConnection(signalServer, false);
  console.log("created peer connection");

  connection.onTrack.subscribe(async (t) => {
    console.log("received track");
    console.log(t);
    console.log(t.streams[0].id);
    video.srcObject = t.streams[0];
  });

  const channel = connection.createDataChannel("pointermove");
  video.addEventListener("pointerdown", (e) => {
    console.log("simple client pointer event", e);
    const h = (e: PointerEvent) => {
      channel.send(JSON.stringify(normalizedPointerEvent(e, video)));
    };
    video.addEventListener("pointermove", h);
    window.addEventListener("pointerup", () => {
      video.removeEventListener("pointermove", h);
    });
  });

  await connection.start();
  console.log("connection started");

  return video;
}

export function createVideoElement(connection: DualDrillConnection) {
  const video = document.createElement("video");
  video.autoplay = true;
  video.playsInline = true;
  video.muted = true;
  connection.onTrack.subscribe(async (t) => {
    console.log("received track");
    console.log(t);
    console.log(t.receiver.getParameters());
    console.log(t.streams[0].id);
    video.srcObject = t.streams[0];
    video.muted = true;
    video.play();
  });
  const channel = connection.createDataChannel("pointermove");
  video.addEventListener("pointerdown", (e) => {
    console.log("simple client pointer event", e);
    const h = (e: PointerEvent) => {
      channel.send(JSON.stringify(normalizedPointerEvent(e, video)));
    };
    video.addEventListener("pointermove", h);
    window.addEventListener("pointerup", () => {
      video.removeEventListener("pointermove", h);
    });
  });
  return video;
}

export function CreateRTCConnection(clientId: string) {
  console.log("create simple RTC client");

  const serverId = "00000000-0000-0000-0000-000000000000";

  const signalServer = createSignalConnectionOverSignalRHubConnection(
    SignalRConnection,
    serverId
  );
  // createSignalServerConnectionOverBlazorInterop()
  const connection = createPeerConnection(signalServer, false);
  console.log("created peer connection");
  const scaleChannel = connection.createDataChannel("scale");
  const el = document.getElementById("scale-input");
  if (el) {
    console.log(`scale input element found`, el);
    el.oninput = (e) => {
      console.log(e);
    };
    el.addEventListener("input", (e) => {
      const value = (e.target as HTMLInputElement).value;
      console.log(`scale set to ${value} event`);
      scaleChannel.send(JSON.stringify({ value: Number.parseFloat(value) }));
    });
  } else {
    console.warn("#scale-input element not found");
  }

  return connection;
}

export function appendChild(el: HTMLElement, child: HTMLElement) {
  el.appendChild(child);
}

export function appendToVideoTarget(el: HTMLElement) {
  document.getElementById("video-target")?.appendChild(el);
}

export async function main() {
  const SignalConnectionBaseUrl = "/api/SignalConnection";
  const serverId: string = (
    await (await fetch(`${SignalConnectionBaseUrl}/server`)).json()
  ).id;
  const clientId = crypto.randomUUID();
  (document.getElementById("client-id-span") as HTMLSpanElement).innerText =
    clientId;

  const webSocket = startWebsocketConnection(clientId);
  const signalConnection = createSignalConnectionOverWebSocket(
    clientId,
    serverId,
    webSocket
  );
  const connection = createPeerConnection(signalConnection, false);

  const video = createVideoElement(connection);
  const div = document.getElementById("video-render-root");
  div?.appendChild(video);

  const camStream = await navigator.mediaDevices.getUserMedia({
    audio: false,
    video: true,
  });
  connection.addTrack(camStream.getVideoTracks()[0], camStream);
}

export async function WebViewMain() {
  const service = new WebView2Service();
  const { surfaceId, buffer, width, height } =
    await service.getSurfaceSharedBuffer();
  const div = document.getElementById("video-render-root");
  const canvas = service.drawSurface(surfaceId);
  div?.appendChild(canvas);
  const stream = canvas.captureStream(60);
  service.MainStream = stream;
  const videoEl = document.getElementById(
    "video-capture-root"
  ) as HTMLVideoElement;
  console.log(videoEl);
  videoEl.playsInline = true;
  videoEl.autoplay = true;
  videoEl.muted = true;
  let setRemote = false;
  setInterval(() => {
    if (!setRemote && service.ReceivedStream) {
      videoEl.srcObject = service.ReceivedStream;
      videoEl.play();
      setRemote = true;
    }
  }, 1000);

  (document.getElementById("renegotiate") as HTMLButtonElement).onclick =
    () => {
      for (const [id, pc] of service.PeerConnections) {
        pc.negotiate();
      }
    };

  onWebviewMessage().subscribe((e) => {
    if (e.type !== Tags.BufferToPresent) {
      console.log(e);
    }
  });

  canvas.addEventListener("pointerdown", (e) => {
    console.log("simple client pointer event", e);
    const h = (e: PointerEvent) => {
      service.send(Tags.PointerEvent, normalizedPointerEvent(e, canvas));
    };
    canvas.addEventListener("pointermove", h);
    window.addEventListener("pointerup", () => {
      canvas.removeEventListener("pointermove", h);
    });
  });
}
