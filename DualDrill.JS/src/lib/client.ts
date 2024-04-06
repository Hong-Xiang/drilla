import { DotNetObject } from "../blazor-dotnet";

export function setStream(video: HTMLVideoElement, stream: MediaStream) {
  console.log(stream);
  video.srcObject = stream;
}

export function createRTCPeerConnection() {
  const connection = new RTCPeerConnection();
  // connection.addEventListener("datachannel", (e) => {
  //   console.log(e.channel);
  //   e.channel.addEventListener("message", (e) => {
  //     console.log(e);
  //   });
  // });
  // const d = connection.createDataChannel("test_system");
  // d.addEventListener("message", (m) => {
  //   console.log(m);
  // });
  // connection.addEventListener("icecandidate", (e) => {
  //   console.log(e);
  // });

  // connection.addEventListener("connectionstatechange", () => {
  //   console.log(`connection state change to ${connection.connectionState}`);
  // });

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
    async waitDataChannel(label: string, tcs: DotNetObject) {
      console.log(`wait data channel label ${label}`);
      let found = false;
      let disposed = false;
      const h = async (e: RTCDataChannelEvent) => {
        console.log(e.channel.label);
        if (!found && e.channel.label === label) {
          found = true;
          disposed = true;
          e.channel.addEventListener("message", (m) => {
            console.log(m);
          });
          console.log(e.channel);
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
    subscribe<K extends keyof RTCPeerConnectionEventMap>(
      dotnetSubject: DotNetObject,
      type: K
    ) {
      console.log(`subscribe called for type ${type}`);
      switch (type) {
        case "connectionstatechange":
          return addEventObserver(
            dotnetSubject,
            "connectionstatechange",
            () => connection.connectionState
          );
        case "icecandidate":
          return addEventObserver(dotnetSubject, "icecandidate", (e) => {
            console.log(e.candidate);
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
        case "datachannel":
          return addEventObserver(
            dotnetSubject,
            "datachannel",
            (e) => e.channel
          );
        default:
          throw new Error(`Not supported type ${type}`);
      }
    },
    getConnectionState: () => connection.connectionState,
    addIceCandidate: async (candidate: RTCIceCandidateInit) => {
      await connection.addIceCandidate(candidate);
    },
    createOffer: async () => {
      console.log("create offer called");
      const offer = await connection.createOffer({
        // offerToReceiveVideo: true,
      });
      await connection.setLocalDescription(offer);
      console.log(offer.sdp);
      return offer.sdp;
    },
    setAnswer: async (sdp: string) => {
      console.log("set answer");
      console.log(sdp);
      await connection.setRemoteDescription({
        type: "answer",
        sdp,
      });
    },
    setOffer: async (sdp: string) => {
      console.log("set offer");
      console.log(sdp);
      await connection.setRemoteDescription({
        type: "offer",
        sdp,
      });
      const answer = await connection.createAnswer();
      await connection.setLocalDescription(answer);
      return answer.sdp;
    },
    createDataChannel: async (label: string) => {
      console.log(`create data channel ${label}`);
      const channel = connection.createDataChannel(label);
      channel.addEventListener("message", (m) => {
        console.log(m);
      });
      return channel;
    },
    dispose: () => {
      connection.close();
    },
  };
}

export function addLogMessageListener(channel: RTCDataChannel) {
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

export function addChannelEventListener(
  target: HTMLElement,
  channel: DotNetObject
) {
  const h = (e: unknown) => {
    channel.invokeMethodAsync("OnNext", 0);
  };
  target.addEventListener("click", h);
  return {
    dispose: () => {
      target.removeEventListener("click", h);
    },
  };
}
