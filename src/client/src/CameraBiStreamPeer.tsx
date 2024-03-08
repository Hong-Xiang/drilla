import { Signal, useSignal } from "@preact/signals";
import { AutoPlayVideo } from "./AutoPlayVideo";
import { PeerSignalServer } from "./peer";
import { useEffect } from "preact/hooks";
import { styled } from "styled-components";

export type RTCRole = "offer" | "answer";

const SideBySideStream = styled.div`
  display: flex;
  width: 100%;
`;

async function cameraStreamInitialization(
  server: PeerSignalServer,
  role: RTCRole,
  selfStream: Signal<MediaStream | null>,
  peerId: string,
  peerStream: Signal<MediaStream | null>,
  cancel: { value: boolean }
) {
  const s = await navigator.mediaDevices.getUserMedia({
    video: true,
  });
  if (cancel.value) {
    return;
  }
  selfStream.value = s;
  if (role === "offer") {
    server.createClientPeer(
      peerId,
      async () => {
        const pc = new RTCPeerConnection();
        const s = selfStream.value;
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
          peerStream.value = t.streams[0];
        };
        return pc;
      },
      {
        offerToReceiveAudio: true,
      }
    );
  }
  if (role === "answer") {
    server.buildAnswerPeer = async () => {
      const pc = new RTCPeerConnection();
      const s = selfStream.value;
      if (s) {
        for (const t of s.getVideoTracks() ?? []) {
          console.log("stream added");
          pc.addTrack(t, s);
        }
      } else {
        console.log("stream not found");
      }
      pc.ontrack = (t) => {
        console.log(t);
        peerStream.value = t.streams[0];
      };
      return pc;
    };
  }
}

export function CameraBiStreamPeer({
  server,
  selfId,
  peerId,
  role,
}: {
  server: PeerSignalServer;
  selfId: string;
  peerId: string;
  role: RTCRole;
}) {
  const selfStream = useSignal<MediaStream | null>(null);
  const peerStream = useSignal<MediaStream | null>(null);

  useEffect(() => {
    const cancel = { value: false };
    cameraStreamInitialization(
      server,
      role,
      selfStream,
      peerId,
      peerStream,
      cancel
    );
    return () => {
      cancel.value = true;
    };
  }, []);

  return (
    <div>
      <h3>{role} Peer</h3>
      <button
        onClick={() => {
          if (role === "offer") {
            server.createClientPeer(peerId, async () => {
              const pc = new RTCPeerConnection();
              const s = selfStream.value;
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
          if (role === "answer") {
            server.buildAnswerPeer = async () => {
              const pc = new RTCPeerConnection();
              pc.ontrack = (t) => {
                console.log(t);
                peerStream.value = t.streams[0];
              };
              return pc;
            };
          }
        }}
      >
        Prepare Connect
      </button>
      <SideBySideStream>
        <div>
          <h3>camera</h3>
          <AutoPlayVideo width={128} stream={selfStream.value}></AutoPlayVideo>
        </div>
        <div>
          <h3>remote</h3>
          <AutoPlayVideo stream={peerStream.value}></AutoPlayVideo>
        </div>
      </SideBySideStream>
    </div>
  );
}
