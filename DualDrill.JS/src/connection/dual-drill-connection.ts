import { createCompositeDisposable } from "../disposable";
import {
  createAsyncMessageSourceFactoryFromEventMap,
  AsyncMessageSource,
} from "../asyncMessage";
import type { SignalConnection } from "./server-signal-connection";

export interface DualDrillConnection extends Disposable {
  createDataChannel: RTCPeerConnection["createDataChannel"];
  onDataChannel: AsyncMessageSource<RTCDataChannelEvent>;
  addTrack: RTCPeerConnection["addTrack"];
  onTrack: AsyncMessageSource<RTCTrackEvent>;
  start(): Promise<void>;
}

const RTCPeerConnectionPublisherFactory =
  createAsyncMessageSourceFactoryFromEventMap<RTCPeerConnectionEventMap>();

export function createPeerConnection(
  signalConnection: SignalConnection
): DualDrillConnection {
  const pc = new RTCPeerConnection();
  const subscriptions = createCompositeDisposable();
  async function negotiate() {
    console.log("negotiate called");
    const offer = await pc.createOffer({
      offerToReceiveVideo: true,
      iceRestart: true,
    });
    if (!offer.sdp) {
      throw new Error("No SDP in offer");
    }
    await pc.setLocalDescription(offer);
    await signalConnection.offer(offer.sdp);
  }

  pc.onnegotiationneeded = negotiate;

  subscriptions.add(
    signalConnection.onOffer.subscribe(async (sdp) => {
      await pc.setRemoteDescription({
        type: "offer",
        sdp,
      });
      const answer = await pc.createAnswer();
      await pc.setLocalDescription(answer);
      await signalConnection.answer(answer.sdp!);
    })
  );

  async function onAnswer(sdp: string) {
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
        pc.onconnectionstatechange = () => {
          if (pc.connectionState === "connected") {
            resolve();
          }
        };
      });
      await negotiate();
      await p;
    },
  };
}
