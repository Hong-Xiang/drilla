import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { Observable, fromEventPattern } from "rxjs";

interface BroadcastMessage {
  sourceId: string;
  label: string;
  data: unknown;
}

export interface PeerSignalServer {
  createServerPeer(
    builder: () => Promise<RTCPeerConnection>,
    offerOptions?: RTCOfferOptions
  ): Promise<RTCPeerConnection>;
  createClientPeer(
    clientId: string,
    builder: () => Promise<RTCPeerConnection>,
    offerOptions?: RTCOfferOptions
  ): Promise<RTCPeerConnection>;

  broadcast(label: string, data: unknown): Promise<void>;
  // sendToClient(id: string, data: unknown): Promise<void>;

  connection: HubConnection;
  broadcast$: Observable<BroadcastMessage>;
  readonly peers: Map<string, RTCPeerConnection>;

  buildAnswerPeer: () => Promise<RTCPeerConnection>;
}

interface WebRTCPeerHub {
  broadcast(label: string, data: unknown): Promise<void>;
  sendClient(id: string, data: unknown): Promise<void>;

  negotiateClient(clientId: string, offerSdp: string): Promise<string>;
  addClientIceCandidate(clientId: string, candidate: string): Promise<void>;
  negotiateServer(offerSdp: string): Promise<string>;
}

export function createPeerSignalServer(url: string): PeerSignalServer {
  const connection = new HubConnectionBuilder().withUrl(url).build();

  const hub: WebRTCPeerHub = {
    broadcast: (label, data) => connection.invoke("Broadcast", label, data),
    sendClient: (id, data) => connection.invoke("SendToClient", id, data),

    negotiateClient: (clientId, offerSdp) =>
      connection.invoke("NegotiateClient", clientId, offerSdp),
    addClientIceCandidate: (clientId, candidate) =>
      connection.invoke("AddClientIceCandidate", clientId, candidate),
    negotiateServer: (offerSdp) =>
      connection.invoke("NegotiateServer", offerSdp),
  };

  const broadcast$ = fromEventPattern<BroadcastMessage>(
    (h) =>
      connection.on("Broadcast", (id, label, data) =>
        h({
          sourceId: id,
          label,
          data,
        })
      ),
    (h) => connection.off("Broadcast", h)
  );

  const peers = new Map<string, RTCPeerConnection>();

  async function createOfferPeer(
    clientId: string | undefined,
    builder: () => Promise<RTCPeerConnection>,
    rtcOfferOptions?: RTCOfferOptions
  ): Promise<RTCPeerConnection> {
    if (clientId) {
      const epc = peers.get(clientId);
      if (epc) {
        return epc;
      }
    }
    const pc = await builder();
    if (clientId) {
      peers.set(clientId, pc);
    }

    connection.on("AddIceCandidate", (cid: string, candidate: string) => {
      if (clientId === cid) {
        const data = JSON.parse(candidate);
        pc.addIceCandidate(new RTCIceCandidate(data));
      }
    });

    pc.addEventListener("icecandidate", (e) => {
      if (clientId) {
        hub.addClientIceCandidate(clientId, JSON.stringify(e.candidate));
      }
    });

    const offer = await pc.createOffer(rtcOfferOptions);
    await pc.setLocalDescription(offer);
    const answer = clientId
      ? await hub.negotiateClient(clientId, offer.sdp!)
      : await hub.negotiateServer(offer.sdp!);
    await pc.setRemoteDescription({
      type: "answer",
      sdp: answer,
    });
    console.log(answer);
    return pc;
  }

  async function createAnswerPeer(
    clientId: string,
    offerSdp: string,
    builder: () => Promise<RTCPeerConnection>
  ): Promise<RTCPeerConnection> {
    if (clientId) {
      const epc = peers.get(clientId);
      if (epc) {
        return epc;
      }
    }
    const pc = await builder();
    if (clientId) {
      peers.set(clientId, pc);
    }
    connection.on(
      "AddIceCandidate",
      async (clientId: string, candidate: string) => {
        const data = JSON.parse(candidate);
        await pc.addIceCandidate(new RTCIceCandidate(data));
      }
    );
    pc.onicecandidate = async (e) => {
      await hub.addClientIceCandidate(clientId, JSON.stringify(e.candidate));
    };
    await pc.setRemoteDescription({
      type: "offer",
      sdp: offerSdp,
    });
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    pc.ondatachannel = (e) => {
      e.channel.onmessage = (m) => {
        console.log(m);
      };
      e.channel.send("ping");
    };
    return pc;
  }

  const result: PeerSignalServer = {
    createClientPeer: async (clientId, builder, options) => {
      return await createOfferPeer(clientId, builder, options);
    },
    broadcast: async (label, data) => {
      await hub.broadcast(label, data);
    },
    broadcast$,
    peers,
    buildAnswerPeer: async () => {
      return new RTCPeerConnection();
    },
    createServerPeer: (builder, options) =>
      createOfferPeer(undefined, builder, options),
    connection: connection,
  };

  connection.on("Negotiate", async (clientId: string, offerSdp: string) => {
    console.log(`[RECV] offer from ${clientId}`);
    const pc = await createAnswerPeer(
      clientId,
      offerSdp,
      result.buildAnswerPeer
    );
    const answer = pc.localDescription?.sdp ?? "";
    console.log(answer);
    return answer;
  });

  return result;
}
