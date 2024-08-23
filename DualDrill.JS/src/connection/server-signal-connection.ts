import type { Observable } from "rxjs";

export interface SignalConnection {
  id: string;
  offer(sdp: string): Promise<void>;
  onOffer: Observable<string>;

  answer(answer: string): Promise<void>;
  onAnswer: Observable<string>;

  addIceCandidate(candidate: RTCIceCandidate): Promise<void>;
  onAddIceCandidate: Observable<RTCIceCandidate>;
}
