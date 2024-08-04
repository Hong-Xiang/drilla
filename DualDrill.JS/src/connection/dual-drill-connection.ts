import { label } from "three/examples/jsm/nodes/Nodes.js";
import { createCompositeDisposable } from "../disposable";
import {
  createAsyncPublisherFactoryFromEventMap,
  IAsyncPublisher,
} from "../pubsub";
import { SignalServerConnection } from "./server-signal-connection";

export interface DualDrillConnection extends Disposable {
  createDataChannel: RTCPeerConnection["createDataChannel"];
  onDataChannel: IAsyncPublisher<RTCDataChannelEvent>;
  addTrack: RTCPeerConnection["addTrack"];
  onTrack: IAsyncPublisher<RTCTrackEvent>;
  start(): Promise<void>;
}

const RTCPeerConnectionPublisherFactory =
  createAsyncPublisherFactoryFromEventMap<RTCPeerConnectionEventMap>();

export function createServerConnection(
  signalServerConnection: SignalServerConnection
): DualDrillConnection {
  const pc = new RTCPeerConnection();
  async function negotiate() {
    console.log('negotiate called')
    const offer = await pc.createOffer({
      offerToReceiveVideo: true,
      iceRestart: true,
    });
    if (!offer.sdp) {
      throw new Error("No SDP in offer");
    }
    await pc.setLocalDescription(offer);
    const answer = await signalServerConnection.negotiate(offer.sdp);
    if (!answer) {
      throw new Error("No SDP in answer");
    }
    await pc.setRemoteDescription({ type: "answer", sdp: answer });
  }

  pc.onnegotiationneeded = negotiate;
  pc.onicecandidate = (e) => {
    if (e && e.candidate) {
      signalServerConnection.addIceCandidate(e.candidate);
    }
  };

  const subscriptions = createCompositeDisposable();
  subscriptions.add(
    signalServerConnection.onicecandidate.subscribe(async (e) => {
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
