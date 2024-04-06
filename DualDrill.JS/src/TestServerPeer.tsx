import { useState } from "preact/hooks";
import { PeerSignalServer } from "./peer";
import { useSignal } from "@preact/signals";
import { AutoPlayVideo } from "./AutoPlayVideo";

export function TestServerPeer({ server }: { server: PeerSignalServer }) {
  const [withServerVideo, setWithServerVideo] = useState(false);
  const stream = useSignal<MediaStream | null>(null);
  return (
    <div>
      <button
        onClick={() => {
          setWithServerVideo(true);
          server.createServerPeer(
            async () => {
              const pc = new RTCPeerConnection();
              pc.ontrack = (e) => {
                stream.value = e.streams[0];
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
        <AutoPlayVideo stream={stream.value}></AutoPlayVideo>
      ) : (
        <></>
      )}
    </div>
  );
}
