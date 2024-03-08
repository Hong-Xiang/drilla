import { useEffect, useState } from "react";

export function AutoPlayVideo({
  stream,
  width,
}: {
  stream: MediaStream | null;
  width?: number;
}) {
  const [video, setVideo] = useState<HTMLVideoElement | null>(null);
  useEffect(() => {
    if (video !== null) {
      video.srcObject = stream;
    }
  }, [video, stream]);
  return <video width={width} ref={setVideo} autoPlay playsInline></video>;
}
