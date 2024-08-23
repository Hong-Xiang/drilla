import { map, Observable } from "rxjs";
import { WebSocketSubject } from "rxjs/webSocket";

export interface TaggedEvent<Tag extends string, Data> {
  readonly type: Tag;
  readonly data: Data;
}

export const Tags = {
  OfferEvent:
    "DualDrill.Engine.Event.ConnectionEvent<DualDrill.Engine.Connection.OfferEvent>" as const,
  AnswerEvent:
    "DualDrill.Engine.Event.ConnectionEvent<DualDrill.Engine.Connection.AnswerEvent>" as const,
  AddIceCandidateEvent:
    "DualDrill.Engine.Event.ConnectionEvent<DualDrill.Engine.Connection.AddIceCandidateEvent>" as const,
  BufferToPresent: "DualDrill.WebView.Event.BufferToPresentEvent" as const,
  PointerEvent: "DualDrill.Engine.Event.PointeEvent" as const,
  RequestPeerConnectionEvent:
    "DualDrill.Engine.Event.ConnectionEvent<DualDrill.WebView.Event.RequestPeerConnectionEvent>" as const,
};

export interface ConnectionEvent<T> {
  readonly sourceId: string;
  readonly targetId: string;
  readonly data: T;
}

export interface BuiltinEventData {
  [Tags.OfferEvent]: ConnectionEvent<{ sdp: string }>;
  [Tags.AnswerEvent]: ConnectionEvent<{ sdp: string }>;
  [Tags.AddIceCandidateEvent]: ConnectionEvent<{ candidate: string }>;
  [Tags.BufferToPresent]: {
    offset: number;
    length: number;
    width: number;
    height: number;
  };
  [Tags.PointerEvent]: {
    x: number;
    y: number;
    surfaceWidth: number;
    surfaceHeight: number;
  };
  [Tags.RequestPeerConnectionEvent]: ConnectionEvent<{}>;
}

export type BuiltinEventType = {
  [key in keyof BuiltinEventData]: string;
};

export type DualDrillEvent<Tag extends keyof BuiltinEventData> = TaggedEvent<
  Tag,
  BuiltinEventData[Tag]
>;

export type HostEvent = DualDrillEvent<typeof Tags.OfferEvent>;

export function webSocketTaggedEvent<Tag extends keyof BuiltinEventData>(
  webSocketSubject: WebSocketSubject<HostEvent>,
  tag: Tag
): Observable<DualDrillEvent<Tag>["data"]> {
  return webSocketSubject
    .multiplex(
      () => {},
      () => {},
      (msg) => {
        console.log(msg);
        console.log(msg.type, tag);
        return msg.type === tag;
      }
    )
    .pipe(map((m) => (m as DualDrillEvent<Tag>).data));
}
