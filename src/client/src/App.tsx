import { useState } from "preact/hooks";
import { Component } from "preact";
import { signal } from "@preact/signals";
import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import "./App.css";
import { ClientPeer, OfferPeer, RTCRole } from "./rtc";
import { createPeerSignalServer } from "./peer";
import { AutoPlayVideo } from "./cam";

export class App extends Component<{}, {}> {
  readonly signalServer = createPeerSignalServer("/hub/rtc");
  readonly clientId = signal<string | null>(null);
  readonly stream = signal<MediaStream | null>(null);
  connectionState = signal(HubConnectionState.Disconnected);

  componentDidMount() {
    (async () => {
      this.connectionState.value = HubConnectionState.Connecting;
      await this.signalServer.connection.start();
      this.clientId.value = this.signalServer.connection.connectionId;
      console.log(this.signalServer.connection.connectionId);
      await this.signalServer.broadcast("test-object", { test: "data" });
      await this.signalServer.broadcast('test-string', "string data test");
    })();
  }

  componentWillUnmount(): void {
    this.signalServer.connection.stop();
  }

  render = () => {
    const [role, setRole] = useState<RTCRole | null>(null);
    const [withServerVideo, setWithServerVideo] = useState(false);

    return (
      <div>
        <h1>Drilla Engine</h1>
        <div>
          <span>clientId {this.clientId} </span>{" "}
          <button
            onClick={() => {
              setWithServerVideo(true);
              this.signalServer.createServerPeer(
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
            }}
          >
            test server peer
          </button>
          {withServerVideo ? (
            <AutoPlayVideo stream={this.stream.value}></AutoPlayVideo>
          ) : (
            <></>
          )}
        </div>

        {role === null ? (
          <>
            <button
              onClick={() => {
                setRole("offer");
              }}
            >
              Start As Offer
            </button>
            <button
              onClick={() => {
                setRole("client");
              }}
            >
              Start As Client
            </button>
          </>
        ) : role === "offer" ? (
          <OfferPeer
            hubConnection={this.signalServer.connection}
            signalServer={this.signalServer}
          ></OfferPeer>
        ) : (
          <ClientPeer
            hubConnection={this.signalServer.connection}
            peerSignalServer={this.signalServer}
          ></ClientPeer>
        )}
      </div>
    );
  };
}
