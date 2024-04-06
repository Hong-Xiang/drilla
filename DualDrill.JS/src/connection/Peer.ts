import { RTCHubServer } from "./RTCHub";

export class LocalPeer implements Disposable {
  readonly connection = new RTCPeerConnection();
  constructor(
    public readonly selfId: string,
    public readonly peerId: string,
    public readonly hub: RTCHubServer
  ) {
    this.connection.onicecandidate = async (e) => {
      if (e.candidate !== null) {
        await this.hub.addIceCandidate(this.peerId, e.candidate);
      }
    };
    this.connection.onnegotiationneeded = async () => {
      await this.hub.connect(selfId, this.peerId);
    };
  }

  async createOffer(): Promise<string | undefined> {
    const offer = await this.connection.createOffer();
    await this.connection.setLocalDescription(offer);
    return offer.sdp;
  }

  async createAnswer(sdp?: string): Promise<string | undefined> {
    await this.connection.setRemoteDescription({
      type: "offer",
      sdp,
    });
    const answer = await this.connection.createAnswer();
    await this.connection.setLocalDescription(answer);
    return answer.sdp;
  }

  async waitConnected(): Promise<void> {
    if (this.connection.connectionState === "connected") {
      return;
    }
    await new Promise<void>((resolve, reject) => {
      const h = () => {
        if (this.connection.connectionState === "connected") {
          this.connection.removeEventListener("connectionstatechange", h);
          resolve();
        }
      };
      this.connection.addEventListener("connectionstatechange", h);
    });
  }

  async setRemoteDescription(
    type: "answer" | "offer",
    sdp?: string
  ): Promise<void> {
    await this.connection.setRemoteDescription({
      type,
      sdp,
    });
  }

  async addIceCandidate(candidate: RTCIceCandidate): Promise<void> {
    await this.connection.addIceCandidate(candidate);
  }
  [Symbol.dispose]() {
    this.connection.close();
  }
}

export class PeersService implements Disposable {
  private readonly clients = new Map<string, LocalPeer>();

  private hub?: RTCHubServer;
  private validateHub(hub: RTCHubServer) {
    if (this.hub && this.hub !== hub) {
      throw Error(`invalid hub`);
    }
    this.hub = hub;
  }

  constructor(private readonly selfId: string) {}

  peer(peerId: string, server: RTCHubServer): LocalPeer {
    this.validateHub(server);

    const client = this.clients.get(peerId);
    if (client) {
      return client;
    } else {
      const newClient = new LocalPeer(this.selfId, peerId, server);
      this.clients.set(peerId, newClient);
      return newClient;
    }
  }
  [Symbol.dispose]() {
    for (const client of this.clients.values()) {
      client[Symbol.dispose]();
    }
  }
}
