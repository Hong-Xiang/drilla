import { HubConnection } from "@microsoft/signalr";
import { Observable, fromEventPattern, share } from "rxjs";
import { LocalPeer, PeersService } from "./Peer";
import { fromHubMessage } from "./HubConnection";

interface PeerMessage {
  sourceConnectionId: string;
}

export interface BroadcastMessage extends PeerMessage {
  label: string;
  data: unknown;
}

export class RTCHubClient implements Disposable {
  constructor(
    private readonly connection: HubConnection,
    private readonly peers: PeersService,
    private readonly server: RTCHubServer
  ) {
    this.connection.on("CreateOffer", this.createOffer);
    this.connection.on("CreateAnswer", this.createAnswer);
    this.connection.on("AddIceCandidate", this.addIceCandidate);
    this.connection.on("WaitConnected", this.addIceCandidate);
  }

  async createOffer(connectionId: string): Promise<string | undefined> {
    const client = this.peers.peer(connectionId, this.server);
    return await client.createOffer();
  }

  async waitConnected(connectionId: string) {
    const client = this.peers.peer(connectionId, this.server);
    return await client.waitConnected();
  }

  async createAnswer(
    connectionId: string,
    sdp?: string
  ): Promise<string | undefined> {
    const client = this.peers.peer(connectionId, this.server);
    return await client.createAnswer(sdp);
  }

  async addIceCandidate(
    connectionId: string,
    candidate: RTCIceCandidate
  ): Promise<void> {
    const client = this.peers.peer(connectionId, this.server);
    await client.addIceCandidate(candidate);
  }

  async setRemoteDescription(
    peerId: string,
    type: "offer" | "answer",
    sdp?: string
  ) {
    const client = this.peers.peer(peerId, this.server);
    await client.setRemoteDescription(type, sdp);
  }

  [Symbol.dispose]() {
    this.connection.off("CreateOffer", this.createOffer);
    this.connection.off("CreateAnswer", this.createAnswer);
    this.connection.off("AddIceCandidate", this.addIceCandidate);
    this.connection.off("WaitConnected", this.addIceCandidate);
  }
}

export class RTCHubServer {
  constructor(private readonly connection: HubConnection) {}

  async broadcast(label: string, data: unknown): Promise<void> {
    await this.connection.send("Broadcast", label, data);
  }

  async negotiate(connectionId: string, sdp: string): Promise<string> {
    return await this.connection.invoke("Negotiate", connectionId, sdp);
  }

  addIceCandidate(
    connectionId: string,
    candidate: RTCIceCandidate
  ): Promise<void> {
    return this.connection.invoke("addIceCandidate", connectionId, candidate);
  }

  async connect(
    offerConnectionId: string,
    answerConnectionId: string
  ): Promise<string> {
    return await this.connection.invoke(
      "Connect",
      offerConnectionId,
      answerConnectionId
    );
  }

  negotiateServer(offerSdp: string): Promise<string> {
    return this.connection.invoke("NegotiateServer", offerSdp);
  }
}
