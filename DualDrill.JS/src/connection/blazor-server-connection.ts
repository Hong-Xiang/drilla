import type { SignalConnection } from "./server-signal-connection";
import type { AsyncMessageEmitter } from "../asyncMessage";

export function createSignalServerConnectionOverBlazorInterop(
  serverInterop: any,
  peerId: string,
  onOffer: AsyncMessageEmitter<string>,
  onAnswer: AsyncMessageEmitter<string>,
  onicecandidate: AsyncMessageEmitter<RTCIceCandidate>
): SignalConnection {
  return {
    id: peerId,
    answer: async (sdp: string) => {
      await serverInterop.invokeMethod("Answer", sdp);
    },
    onAnswer: onAnswer.messageSource,
    offer: async (sdp) => {
      await serverInterop.invokeMethod("Offer", sdp);
    },
    onOffer: onOffer.messageSource,
    addIceCandidate: async (candidate: RTCIceCandidate) => {
      return await serverInterop.invokeMethod(
        "AddIceCandidate",
        JSON.stringify(candidate)
      );
    },
    onAddIceCandidate: onicecandidate.messageSource,
  };
}
