import { Component } from "preact";
import { signal } from "@preact/signals";
import { HubConnectionState } from "@microsoft/signalr";
import "./App.css";
import { CameraBiStreamPeer, RTCRole } from "./CameraBiStreamPeer";
import { createPeerSignalServer } from "./peer";
import { Subject, interval, takeUntil } from "rxjs";
import { TestServerPeer } from "./TestServerPeer";
import { PoseMonitor } from "./PoseMonitor";

export class App extends Component<{}, {}> {
  readonly server = createPeerSignalServer("/hub/rtc");
  readonly selfId = signal<string | null>(null);
  readonly peerId = signal<string | null>(null);
  readonly role = signal<RTCRole | null>(null);
  readonly done$ = new Subject<{}>();
  readonly stream = signal<MediaStream | null>(null);
  connectionState = signal(HubConnectionState.Disconnected);

  componentDidMount() {
    (async () => {
      this.connectionState.value = HubConnectionState.Connecting;
      await this.server.connection.start();
      this.connectionState.value = this.server.connection.state;
      this.selfId.value = this.server.connection.connectionId;
      console.log(this.server.connection.connectionId);

      interval(500)
        .pipe(takeUntil(this.done$))
        .subscribe((_) => {
          if (this.role.value !== null) {
            this.server.broadcast(this.role.value, this.selfId.value);
          }
        });
      this.server.broadcast$.pipe(takeUntil(this.done$)).subscribe((m) => {
        if (
          (m.label === "offer" || m.label === "answer") &&
          m.label !== this.role.value &&
          this.role.value !== null
        ) {
          this.peerId.value = m.data as string;
        }
      });
    })();
  }

  componentWillUnmount(): void {
    this.server.connection.stop();
    this.connectionState.value = this.server.connection.state;
    this.done$.next({});
    this.done$.complete();
  }

  render = () => {
    return (
      <div>
        <h1>Drilla Engine</h1>
        <a href="render-tutorial.html">render page</a>
        <PoseMonitor></PoseMonitor>
        <div>
          <div>Self Id: {this.selfId}</div>
          <div>Peer Id: {this.peerId}</div>
        </div>
        <TestServerPeer server={this.server}></TestServerPeer>

        {this.role.value === null ? (
          <>
            <button
              onClick={() => {
                this.role.value = "offer";
              }}
            >
              Start As Offer
            </button>
            <button
              onClick={() => {
                this.role.value = "answer";
              }}
            >
              Start As Answer
            </button>
          </>
        ) : (
          <></>
        )}
        {this.role.value !== null &&
        this.selfId.value !== null &&
        this.peerId.value !== null ? (
          <CameraBiStreamPeer
            server={this.server}
            selfId={this.selfId.value}
            peerId={this.peerId.value}
            role={this.role.value}
          ></CameraBiStreamPeer>
        ) : (
          <></>
        )}
      </div>
    );
  };
}
