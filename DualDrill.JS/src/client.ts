import { Subject, filter, first, fromEvent, map } from "rxjs";
import { DotNetObject } from "./blazor-dotnet";
import {
  PromiseLikeResultMapper,
  subscribeByPromiseLike,
} from "./lib/dotnet-server-interop";
import { SignalRConnection } from "./lib/signalr-client";
import * as signalR from "@microsoft/signalr";

export { getProperty, setProperty } from "./lib/dotnet-server-interop";
// export { createWebGPURenderService } from "../render/RenderService";
export { createWebGPURenderService } from "./webgpu/rotateCube";
export { createServerRenderPresentService } from "./render/DistributeRenderService";
export { createHeadlessSharedBufferServerRenderService } from "./render/headlessSharedBufferServerRenderService";
export { createHeadlessServerRenderService } from "./render/headlessRenderService";
export { getDotnetWasmExports } from "./lib/jsexport-client";
import { getDotnetWasmExports } from "./lib/jsexport-client";

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

export async function Initialization(blazorServerService?: DotNetObject) {
  console.log("initialization called");
  await SignalRConnection.start();
  if (blazorServerService) {
    BlazorServerService = blazorServerService;
  }

  const subject = new signalR.Subject<number>();
  let count = 0;
  const h = setInterval(() => {
    subject.next(count);
    count++;
  }, 1000);

  SignalRConnection.stream("PingPongStream", subject).subscribe({
    next: (value) => {
      console.log(`pong ${value}`);
    },
    error: (e) => {
      console.error(e);
    },
    complete: () => {
      console.log("ping pong channel complete");
    },
  });

  // async function testFetchRenderResult() {
  //   const res = await fetch("/api/vulkan/render");
  //   const b = await res.blob();
  //   console.log(b.size);

  //   requestAnimationFrame(testFetchRenderResult);
  // }

  // requestAnimationFrame(testFetchRenderResult);
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

  const done = new Subject<null>();
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
  return canvas.captureStream(30);
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

export async function CreateSimpleRTCClient(divE: HTMLDivElement) {
  const video = document.createElement("video");
  video.autoplay = true;
  video.playsInline = true;
  divE.appendChild(video);

  const pc = new RTCPeerConnection({ iceServers: [] });

  //pc.ontrack = evt => document.querySelector('#videoCtl').srcObject = evt.streams[0];
  pc.onicecandidate = (evt) => {
    // if (evt.candidate) {
    //   fetch("api/rtcclient/candidate", {
    //     method: "POST",
    //     headers: {
    //       "Content-Type": "text/plain",
    //     },
    //     body: JSON.stringify(evt.candidate),
    //   });
    // }
  };

  pc.ontrack = (t) => {
    console.log("received track");
    video.srcObject = t.streams[0];
  };

  // Diagnostics.
  pc.onicegatheringstatechange = () =>
    console.log("onicegatheringstatechange: " + pc.iceGatheringState);
  pc.oniceconnectionstatechange = () =>
    console.log("oniceconnectionstatechange: " + pc.iceConnectionState);
  pc.onsignalingstatechange = () =>
    console.log("onsignalingstatechange: " + pc.signalingState);
  pc.onconnectionstatechange = () =>
    console.log("onconnectionstatechange: " + pc.connectionState);

  const offer = await pc.createOffer({
    offerToReceiveVideo: true,
  });
  await pc.setLocalDescription(offer);
  const answer = await fetch(`api/rtcclient`, {
    method: "POST",
    headers: {
      "Content-Type": "text/plain",
    },
    body: offer.sdp,
  });
  const answerSdp = await answer.text();
  console.log("got answer", answerSdp);
  await pc.setRemoteDescription({
    type: "answer",

    sdp: answerSdp,
  });
  return video;
}
