import { useEffect, useState } from "react";

export function AutoPlayVideo({ stream }: { stream: MediaStream | null }) {
  const [video, setVideo] = useState<HTMLVideoElement | null>(null);
  useEffect(() => {
    if (video !== null) {
      video.srcObject = stream;
    }
  }, [video, stream]);
  return <video ref={setVideo} autoPlay playsInline></video>;
}
