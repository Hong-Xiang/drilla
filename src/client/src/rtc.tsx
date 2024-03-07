import { signal } from "@preact/signals";
import { HubConnection } from "@microsoft/signalr";
import { AutoPlayVideo } from "./cam";
import { Component } from "preact";
import { SignalServer } from "./peer";
import { interval } from "rxjs";

export type RTCRole = "offer" | "client";
export interface RTCSession {
  readonly connection: RTCPeerConnection;
  readonly role: RTCRole;
}

interface ServerRTCConnection {
  readonly connection: RTCPeerConnection;
  readonly channel: RTCDataChannel;
  start(): Promise<void>;
  close(): void;
}

function createServerRTCConnection(
  url: string,
  onMessage: (data: string, channel: RTCDataChannel) => void
) {
  const connection = new RTCPeerConnection();
  const channel = connection.createDataChannel("data", {
    ordered: true,
  });
  channel.onmessage = (msg) => {
    console.log(msg);
    onMessage(msg.data, channel);
  };
  return {
    connection,
    channel,
    start: async () => {
      const offer = await connection.createOffer({
        offerToReceiveVideo: true,
      });
      await connection.setLocalDescription(offer);
      console.log("peer -> server offer");
      console.log(offer.sdp);
      const r = await fetch(url, {
        method: "POST",
        body: offer.sdp,
      });
      const answer = await r.text();
      console.log("peer <- server answer");
      console.log(answer);
      await connection.setRemoteDescription({
        type: "answer",
        sdp: answer,
      });
    },
    close: () => {
      channel.close();
      connection.close();
    },
  };
}

export class OfferPeer extends Component<
  { hubConnection: HubConnection; signalServer: SignalServer },
  {}
> {
  readonly pushConnection = new RTCPeerConnection();
  readonly stream = signal<MediaStream | null>(null);
  readonly clientId = signal<string | null>(null);
  readonly connection = createServerRTCConnection(
    "/api/render/push",
    async (data, channel) => {
      this.pushConnection.onconnectionstatechange = (e) => {
        console.log(e);
      };
      this.pushConnection.oniceconnectionstatechange = (e) => {
        console.log(this.pushConnection.iceConnectionState);
      };
      this.pushConnection.onicecandidate = (e) => {
        console.log(e);
        channel.send(
          JSON.stringify({
            kind: "candidate",
            data: e.candidate,
          })
        );
      };
      this.pushConnection.onnegotiationneeded = (e) => {
        console.log(e);
      };
      console.log(data);
      const m = JSON.parse(data);
      console.log(m);
      switch (m.kind) {
        case "link":
          const s = this.stream.value;
          if (s) {
            for (const t of s.getVideoTracks() ?? []) {
              console.log("stream added");
              this.pushConnection.addTrack(t, s);
            }
          } else {
            console.log("stream not found");
          }
          const dataChannel =
            this.pushConnection.createDataChannel("render-command");
          this.pushConnection.ondatachannel = (e) => {
            console.log(e);
          };
          dataChannel.onmessage = (msg) => {
            console.log(msg);
            dataChannel.send("push-pull pong");
          };
          const offer = await this.pushConnection.createOffer();
          this.pushConnection.setLocalDescription(offer);
          console.log("push -> pull offer");
          console.log(offer.sdp);
          channel.send(
            JSON.stringify({
              kind: "offer",
              data: offer.sdp,
            })
          );
          break;
        case "candidate":
          if (this.pushConnection.remoteDescription) {
            this.pushConnection.addIceCandidate(m.data);
          }
          break;
        case "answer":
          console.log("push <- pull answer");
          await this.pushConnection.setRemoteDescription({
            type: "answer",
            sdp: m.data,
          });
          break;
        default:
          console.error("unknown msg");
      }
    }
  );

  private dataChannel: RTCDataChannel | undefined = undefined;

  private async cameraInitialization() {
    this.stream.value = await navigator.mediaDevices.getUserMedia({
      video: true,
    });
  }

  readonly RequestOfferName = "RequestOffer" as const;

  createPeer(signalServer: SignalServer) {
    // this.props.signalServer.createServerPeer(async () => {
    //   const pc = new RTCPeerConnection();
    //   const dataChannel = pc.createDataChannel("data");
    //   this.dataChannel = dataChannel;
    //   dataChannel.onmessage = (e) => {
    //     console.log("peer ping");
    //     dataChannel.send("pong");
    //   };
    //   return pc;
    // });
  }

  componentDidMount = () => {
    this.cameraInitialization();
    this.props.hubConnection.on(this.RequestOfferName, this.requestOffer);
    this.props.signalServer.broadcast$.subscribe((x) => {
      if (x.label === "pull") {
        this.clientId.value = x.data as string;
      }
    });

    interval(500).subscribe((_) =>
      this.props.signalServer.broadcast(
        "push",
        this.props.signalServer.connection.connectionId
      )
    );
  };

  private requestOffer(msg: unknown) {
    console.log(msg);
  }

  componentWillUnmount = () => {
    this.props.hubConnection.off(this.RequestOfferName, this.requestOffer);
    this.connection.close();
  };

  render = () => {
    return (
      <div>
        <h3>Offer Peer</h3>
        <input
          type="text"
          value={this.clientId.value ?? ""}
          onInput={(e) => {
            this.clientId.value = e.currentTarget.value;
          }}
        />
        <button
          onClick={async () => {
            const clientId = this.clientId.value;
            if (clientId) {
              this.props.signalServer.createClientPeer(clientId, async () => {
                const pc = new RTCPeerConnection();
                const s = this.stream.value;
                if (s) {
                  for (const t of s.getVideoTracks() ?? []) {
                    console.log("stream added");
                    pc.addTrack(t, s);
                  }
                } else {
                  console.log("stream not found");
                }
                const dataChannel = await pc.createDataChannel("data");
                dataChannel.onmessage = (m) => {
                  console.log(m);
                  dataChannel.send("pong");
                };
                pc.ontrack = (t) => {
                  console.log(t);
                };
                return pc;
              });
            }

            // await this.connection.start();
          }}
        >
          {" "}
          Post Offer
        </button>
        <div>
          <h3>Camera Playback</h3>
          <AutoPlayVideo stream={this.stream.value}></AutoPlayVideo>
        </div>
      </div>
    );
  };
}

export class ClientPeer extends Component<
  { hubConnection: HubConnection; peerSignalServer: SignalServer },
  {}
> {
  readonly clientId = signal<string | null>(null);
  readonly pullConnection = new RTCPeerConnection();
  readonly stream = signal<MediaStream | null>(null);
  readonly connection = createServerRTCConnection(
    "/api/render/pull",
    async (data, channel) => {
      this.pullConnection.onconnectionstatechange = (e) => {
        console.log(e);
      };
      this.pullConnection.onconnectionstatechange = (e) => {
        console.log(e);
      };
      this.pullConnection.oniceconnectionstatechange = (e) => {
        console.log(this.pullConnection.iceConnectionState);
      };
      this.pullConnection.onicecandidate = (e) => {
        console.log(e);
        channel.send(
          JSON.stringify({
            kind: "candidate",
            data: e.candidate,
          })
        );
      };
      this.pullConnection.onnegotiationneeded = (e) => {
        console.log(e);
      };
      this.pullConnection.ontrack = (e) => {
        this.stream.value = e.streams[0];
      };
      console.log("push -> pull offer");
      console.log(data);

      const m = JSON.parse(data);
      console.log(m);
      switch (m.kind) {
        case "offer":
          this.pullConnection.setRemoteDescription({
            type: "offer",
            sdp: m.data,
          });
          this.pullConnection.ondatachannel = (e) => {
            console.log(e);
            e.channel.onmessage = (m) => {
              console.log(m);
            };
            e.channel.send("push-pull ping");
          };
          const ans = await this.pullConnection.createAnswer();
          this.pullConnection.setLocalDescription(ans);
          console.log("push <- pull answer");
          console.log(ans.sdp);
          channel.send(JSON.stringify({ kind: "answer", data: ans.sdp! }));
          break;
        case "candidate":
          if (m.data) {
            this.pullConnection.addIceCandidate(new RTCIceCandidate(m.data));
          }
          break;
        default:
          console.error("unknown message");
      }
    }
  );
  readonly pc = new RTCPeerConnection();
  componentDidMount(): void {
    this.pc.ontrack = (evt) => {
      this.stream.value = evt.streams[0];
    };

    this.props.hubConnection.on("Offer", (msg) => {
      console.log(msg);
    });

    this.props.peerSignalServer.broadcast$.subscribe((x) => {
      if (x.label === "push") {
        this.clientId.value = x.data as string;
      }
    });

    interval(500).subscribe((_) =>
      this.props.peerSignalServer.broadcast(
        "pull",
        this.props.peerSignalServer.connection.connectionId
      )
    );
  }

  componentWillUnmount(): void {
    this.pc.ontrack = null;
    this.pc.close();
  }

  async createPeer(signalServer: SignalServer, clientId: string) {
    const pc = await signalServer.createClientPeer(clientId, async () => {
      const c = new RTCPeerConnection();

      return c;
    });
    pc.ontrack = (e) => {
      this.stream.value = e.streams[0];
    };
  }

  async createServerPeer(
    connection: HubConnection,
    signalServer: SignalServer
  ) {
    const pc = await signalServer.createServerPeer(
      async () => {
        const pc = new RTCPeerConnection();
        pc.ontrack = (e) => {
          this.stream.value = e.streams[0];
        };
        return pc;
      },
      {
        offerToReceiveVideo: true,
      }
    );
  }

  render() {
    return (
      <div>
        <h3>Client Peer</h3>
        <input
          type="text"
          placeholder="input clientId here"
          value={this.clientId.value ?? ""}
          onInput={(e) => {
            this.clientId.value = e.currentTarget.value;
          }}
        />
        <button
          onClick={() => {
            this.props.peerSignalServer.buildAnswerPeer = async () => {
              const pc = new RTCPeerConnection();
              pc.ontrack = (t) => {
                console.log(t);
                this.stream.value = t.streams[0];
              };
              return pc;
            };
          }}
        >
          Prepare Connect
        </button>
        <AutoPlayVideo stream={this.stream.value}></AutoPlayVideo>
      </div>
    );
  }
}
