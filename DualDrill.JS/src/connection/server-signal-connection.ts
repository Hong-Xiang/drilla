import type { Observable } from "rxjs";
import { type AsyncMessageSource } from "../asyncMessage";

export interface SignalConnection {
  id: string;
  offer(sdp: string): Promise<void>;
  onOffer: AsyncMessageSource<string>;

  answer(answer: string): Promise<void>;
  onAnswer: AsyncMessageSource<string>;

  addIceCandidate(candidate: RTCIceCandidate): Promise<void>;
  onAddIceCandidate: AsyncMessageSource<RTCIceCandidate>;
}
