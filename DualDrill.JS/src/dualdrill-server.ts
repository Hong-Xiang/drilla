import type { HubConnection } from "@microsoft/signalr";

interface IAsyncPublisher<T> {
  subscribe(f: (evt: T) => Promise<void>): Disposable;
}

export interface ServerSignalConnection {
  negotiate(offerSdp: string): Promise<string>;
  addIceCandidate(candidate: RTCIceCandidate): Promise<void>;
  onicecandidate: IAsyncPublisher<RTCIceCandidate>;
  onnegotiationneeded: IAsyncPublisher<void>;
}

function createServerSignalConnectionFromSignalRHubConnection(
  hubConnection: HubConnection
): ServerSignalConnection {
  const negotiate = async (offerSdp: string) => {
    return await hubConnection.invoke("Negotiate", offerSdp);
  };
  return {
    negotiate,
    addIceCandidate: async (candidate: RTCIceCandidate) => {
      return await hubConnection.invoke("AddIceCandidate", candidate);
    },
    onicecandidate: {
      subscribe: (e) => {
        const name = "IceCandidate";
        hubConnection.on(name, e);
        return {
          [Symbol.dispose]: () => {
            hubConnection.off(name, e);
          },
        };
      },
    },
    onnegotiationneeded: {
      subscribe: () => {
        const name = "negotiationneeded";
        hubConnection.on(name, negotiate);
        return {
          [Symbol.dispose]: () => {
            hubConnection.off(name, negotiate);
          },
        };
      },
    },
  };
}

export interface ServerConnection extends Disposable {
  createDataChannel: RTCPeerConnection["createDataChannel"];
  onDataChannel: IAsyncPublisher<RTCDataChannelEvent>;
  addTrack: RTCPeerConnection["addTrack"];
  onTrack: IAsyncPublisher<RTCTrackEvent>;
}

function createServerConnection(
  signalConnection: ServerSignalConnection
): ServerConnection {
  const pc = new RTCPeerConnection();
  pc.onnegotiationneeded = async () => {
    const offer = await pc.createOffer({
      offerToReceiveVideo: true,
    });
    pc.setLocalDescription(offer);
  };
  return {
    createDataChannel: pc.createDataChannel,
    onDataChannel: {
      subscribe: (e) => {
        pc.addEventListener("datachannel", e);
        return {
          [Symbol.dispose]: () => {
            pc.removeEventListener("datachannel", e);
          },
        };
      },
    },
    addTrack: pc.addTrack,
    onTrack: {
      subscribe: (e) => {
        pc.addEventListener("track", e);
        return {
          [Symbol.dispose]: () => {
            pc.removeEventListener("track", e);
          },
        };
      },
    },
    [Symbol.dispose]: () => {
      pc.close();
    },
  };
}
