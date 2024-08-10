import type { HubConnection } from "@microsoft/signalr";
import type { SignalConnection } from "./server-signal-connection";

export function createSignalConnectionOverSignalRHubConnection(
  hubConnection: HubConnection,
  peerId: string
): SignalConnection {
  return {
    id: peerId,
    async offer(sdp) {
      console.log(`offer(${peerId}): ${sdp}`);
      await hubConnection.invoke("Offer", peerId, sdp);
    },
    onOffer: {
      subscribe: (e) => {
        const name = "Offer";
        const handler = (source: string, sdp: string) => {
          console.log(source, sdp);
          if (source === peerId) {
            e(sdp);
          } else {
            console.warn(`onOffer: source=${source}, peerId=${peerId}`);
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
    answer: async (answer: string): Promise<void> => {
      console.log(`answer(${peerId}): ${answer}`);
      await hubConnection.invoke("Answer", peerId, answer);
    },
    onAnswer: {
      subscribe: (e) => {
        const name = "Answer";
        const handler = (source: string, sdp: string) => {
          console.log(source, sdp);
          if (source === peerId) {
            e(sdp);
          } else {
            console.warn(`onAnswer: source=${source}, peerId=${peerId}`);
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
    addIceCandidate: async (candidate: RTCIceCandidate) => {
      console.log(`addIceCandidate(${peerId}): ${candidate}`);
      return await hubConnection.invoke(
        "AddIceCandidate",
        peerId,
        JSON.stringify(candidate)
      );
    },
    onAddIceCandidate: {
      subscribe: (e) => {
        const name = "AddIceCandidate";
        const handler = (source: string, candidate: string) => {
          console.log(source, candidate);
          if (source === peerId && candidate) {
            e(JSON.parse(candidate));
          } else {
            console.warn(
              `onAddIceCandidate: source=${source}, peerId=${peerId}`
            );
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
  };
}
