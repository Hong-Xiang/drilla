import { webSocket, WebSocketSubject } from "rxjs/webSocket";
import { SignalConnection } from "./server-signal-connection";
import { HostEvent, Tags, webSocketTaggedEvent } from "../taggedEvent";
import { filter, map, tap } from "rxjs";

export function startWebsocketConnection(clientId: string) {
  var scheme = document.location.protocol === "https:" ? "wss" : "ws";
  var port = document.location.port ? ":" + document.location.port : "";

  const connectionUrl =
    scheme +
    "://" +
    document.location.hostname +
    port +
    `/ws/signal-connection/${clientId}`;
  //
  // const socket = new WebSocket(connectionUrl);

  //   socket.onmessage = (e) => {};

  //   socket.onopen = (e) => {
  //     console.log(e);
  //   };
  //   socket.onclose = (e) => {
  //     console.log(e);
  //   };
  //   socket.onmessage = (e) => {
  //     console.log(e);
  //   };
  return webSocket<HostEvent>({
    url: connectionUrl,
  });
}

export function createSignalConnectionOverWebSocket(
  clientId: string,
  serverId: string,
  webSocket: WebSocketSubject<HostEvent>
): SignalConnection {
  const baseUrl = "/api/SignalConnection";
  return {
    id: clientId,
    offer: async (sdp) => {
      await fetch(`${baseUrl}/Offer/${clientId}/${serverId}`, {
        method: "POST",
        headers: {
          "Content-Type": "text/plain",
        },
        body: sdp,
      });
    },
    onOffer: webSocketTaggedEvent(webSocket, Tags.OfferEvent).pipe(
      filter((m) => m.targetId === clientId),
      map((m) => m.data.sdp),
      tap((m) => {
        console.log("onOffer", m);
      })
    ),
    answer: async (sdp) => {
      await fetch(`${baseUrl}/Answer/${clientId}/${serverId}`, {
        method: "POST",
        headers: {
          "Content-Type": "text/plain",
        },
        body: sdp,
      });
    },
    onAnswer: webSocketTaggedEvent(webSocket, Tags.AnswerEvent).pipe(
      filter((m) => m.targetId === clientId),
      map((m) => m.data.sdp),
      tap((m) => {
        console.log("onAnswer", m);
      })
    ),
    addIceCandidate: async (candidate) => {
      await fetch(`${baseUrl}/AddIceCandidate/${clientId}/${serverId}`, {
        method: "POST",
        headers: {
          "Content-Type": "text/plain",
        },
        body: JSON.stringify(candidate),
      });
    },
    onAddIceCandidate: webSocketTaggedEvent(
      webSocket,
      Tags.AddIceCandidateEvent
    ).pipe(
      filter((m) => m.targetId === clientId),
      map((m) => {
        console.log(m);
        return JSON.parse(m.data.candidate);
      })
    ),
  };
}
