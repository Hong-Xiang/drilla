import { Subscription } from "rxjs";
import {
  createAsyncMessageSourceFactoryFromEventMap,
  AsyncMessageSource,
} from "../asyncMessage";
import type { SignalConnection } from "./server-signal-connection";

export interface DualDrillConnection extends Disposable {
  readonly peer: RTCPeerConnection;
  negotiate(): Promise<void>;
  createDataChannel: RTCPeerConnection["createDataChannel"];
  onDataChannel: AsyncMessageSource<RTCDataChannelEvent>;
  addTrack: RTCPeerConnection["addTrack"];
  onTrack: AsyncMessageSource<RTCTrackEvent>;
  start(): Promise<void>;
}

const RTCPeerConnectionPublisherFactory =
  createAsyncMessageSourceFactoryFromEventMap<RTCPeerConnectionEventMap>();

export function createPeerConnection(
  signalConnection: SignalConnection,
  subscribeNegotiationNeeded: boolean
): DualDrillConnection {
  const pc = new RTCPeerConnection({
  });
  const subscriptions = new Subscription();
  async function negotiate() {
    console.log(`negotiate called ${signalConnection.id}`);
    const offer = await pc.createOffer({
      offerToReceiveVideo: true,
      // iceRestart: true,
    });
    if (!offer.sdp) {
      throw new Error("No SDP in offer");
    }
    await pc.setLocalDescription(offer);
    await signalConnection.offer(offer.sdp);
  }

  pc.onnegotiationneeded = () => {
    console.warn("negotiation needed");
    console.log(signalConnection.id);
    if (subscribeNegotiationNeeded) {
      negotiate();
    }
  };

  subscriptions.add(
    signalConnection.onOffer.subscribe(async (sdp) => {
      console.log(`on offer called ${signalConnection.id}`);
      var arr = sdp.split("\r\n");
      arr.forEach((str, i) => {
        if (/^a=fmtp:\d*/.test(str)) {
          arr[i] =
            str +
            ";x-google-max-bitrate=28000;x-google-min-bitrate=0;x-google-start-bitrate=20000";
        }
      });
      const modifiedSdp = arr.join("\r\n");
      console.log("got offer");
      console.log(sdp);
      await pc.setRemoteDescription({
        type: "offer",
        sdp: sdp,
      });
      const answer = await pc.createAnswer({});
      console.log("created answer");
      console.log(answer);
      await pc.setLocalDescription(answer);
      await signalConnection.answer(answer.sdp!);
    })
  );

  async function onAnswer(sdp: string) {
    console.log(`on answer ${signalConnection.id}`);
    if (pc.localDescription) {
      await pc.setRemoteDescription({ type: "answer", sdp });
    }
  }
  subscriptions.add(signalConnection.onAnswer.subscribe(onAnswer));

  pc.onicecandidate = (e) => {
    console.log(e);
    if (e && e.candidate) {
      signalConnection.addIceCandidate(e.candidate);
    }
  };

  subscriptions.add(
    signalConnection.onAddIceCandidate.subscribe(async (e) => {
      if (pc.remoteDescription) {
        await pc.addIceCandidate(e);
      }
    })
  );

  pc.addEventListener("connectionstatechange", (e) => {
    console.log(pc.connectionState);
  });

  // Diagnostics.
  pc.onicegatheringstatechange = () =>
    console.log("onicegatheringstatechange: " + pc.iceGatheringState);
  pc.oniceconnectionstatechange = () =>
    console.log("oniceconnectionstatechange: " + pc.iceConnectionState);
  pc.onsignalingstatechange = () =>
    console.log("onsignalingstatechange: " + pc.signalingState);
  pc.onconnectionstatechange = () =>
    console.log("onconnectionstatechange: " + pc.connectionState);

  return {
    peer: pc,
    negotiate,
    createDataChannel: (label, dataChannelDict) =>
      pc.createDataChannel(label, dataChannelDict),
    onDataChannel: RTCPeerConnectionPublisherFactory(pc, "datachannel"),
    addTrack: (track, ...streams) => pc.addTrack(track, ...streams),
    onTrack: RTCPeerConnectionPublisherFactory(pc, "track"),
    [Symbol.dispose]: () => {
      pc.close();
    },
    start: async () => {
      const p = new Promise<void>((resolve) => {
        pc.addEventListener("connectionstatechange", () => {
          if (pc.connectionState === "connected") {
            resolve();
          }
        });
      });
      await negotiate();
      await p;
    },
  };
}
