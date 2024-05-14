import {
  Subject,
  filter,
  first,
  fromEvent,
  fromEventPattern,
  map,
  take,
  takeUntil,
} from "rxjs";
import { DotNetObject } from "./blazor-dotnet";
import {
  PromiseLikeResultMapper,
  createJSObjectReference,
  subscribeByPromiseLike,
} from "./lib/dotnet-server-interop";
import { UUID } from "crypto";
import { SignalRConnection } from "./lib/signalr-client";
import * as signalR from "@microsoft/signalr";

export { getProperty, setProperty } from "./lib/dotnet-server-interop";
// export { createWebGPURenderService } from "../render/RenderService";
export { createWebGPURenderService } from "./webgpu/rotateCube";

export function asObjectReference<T>(x: T) {
  return x;
}

export async function StartSignalRConnection(element: HTMLElement) {
  await SignalRConnection.start();

  const response = await SignalRConnection.invoke("Echo", "Hello");
  console.log(response);
  const subject = new signalR.Subject<{
    Type: string;
    ClientX: number;
    ClientY: number;
  }>();
  SignalRConnection.send("MouseEvent", subject);

  console.log(element);
  element.addEventListener("mousemove", (e) => {
    console.log(e);
    subject.next({
      Type: e.type,
      ClientX: e.clientX,
      ClientY: e.clientY,
    });
  });

  SignalRConnection.stream("RenderStates").subscribe({
    next: (state) => {
      console.log(state);
    },
    error: (e) => {
      console.error(e);
    },
    complete: () => {
      console.log("complete");
    },
  });
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
