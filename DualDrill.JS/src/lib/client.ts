import { filter, first, fromEvent, fromEventPattern, map, take } from "rxjs";
import { DotNetObject } from "../blazor-dotnet";
import {
  PromiseLikeResultMapper,
  createJSObjectReference,
  subscribeByPromiseLike,
} from "./dotnet-server-interop";

export { createWebGPURenderService } from "../render/RenderService";

export function getProperty(
  target: unknown,
  keys: readonly (string | number)[]
): unknown {
  let result: any = target;
  for (const key of keys) {
    result = result[key];
  }
  return result;
}

export function setProperty(
  target: unknown,
  value: unknown,
  keys: readonly (string | number)[]
): void {
  function go(target: any, value: unknown, keys: readonly (string | number)[]) {
    if (keys.length === 0) {
      throw new Error(`keys is empty`);
    }
    const [key, ...rest] = keys;
    if (rest.length === 0) {
      target[key] = value;
      return;
    }
    go(target[key], value, rest);
  }
  go(target, value, keys);
}

export function asObjectReference<T>(x: T) {
  return x;
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
