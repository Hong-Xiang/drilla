import type { HubConnection } from "@microsoft/signalr";
import type { IAsyncPublisher } from "../pubsub";

export interface SignalServerConnection {
  negotiate(offerSdp: string): Promise<string>;
  onnegotiationneeded: IAsyncPublisher<void>;

  addIceCandidate(candidate: RTCIceCandidate): Promise<void>;
  onicecandidate: IAsyncPublisher<RTCIceCandidate>;
}

export function createSignalServerConnectionFromSignalRHubConnection(
  hubConnection: HubConnection
): SignalServerConnection {
  const negotiate = async (offerSdp: string) => {
    return await hubConnection.invoke("Negotiate", offerSdp);
  };
  return {
    negotiate,
    addIceCandidate: async (candidate: RTCIceCandidate) => {
      return await hubConnection.invoke(
        "AddIceCandidate",
        JSON.stringify(candidate)
      );
    },
    onicecandidate: {
      subscribe: (e) => {
        const name = "IceCandidate";
        const handler = (candidate: string) => {
          if (candidate) {
            e(JSON.parse(candidate));
          }
        };
        hubConnection.on(name, handler);
        return {
          [Symbol.dispose]: () => {
            hubConnection.off(name, handler);
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
