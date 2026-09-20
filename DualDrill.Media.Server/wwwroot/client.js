// @ts-check

/**
 * @typedef {{type: "offer", sdp: string}
 * | {type: "ice", candidate: string, sdpMLineIndex: number}
 * | {type: "status" | "error", message: string}} Signal
 * @typedef {{peer: RTCPeerConnection, socket: WebSocket,
 * pending: RTCIceCandidateInit[], signals: Promise<void>}} Attempt
 */

const video = document.querySelector("video");
const status = document.querySelector("#status");
const start = document.querySelector("#start");
const stop = document.querySelector("#stop");
if (
  !(video instanceof HTMLVideoElement) ||
  !(status instanceof HTMLElement) ||
  !(start instanceof HTMLButtonElement) ||
  !(stop instanceof HTMLButtonElement)
) {
  throw new Error("The viewer's required elements are missing.");
}
const ui = { video, status, start, stop };
/** @type {Attempt | null} */
let active = null;

/** @param {string} text @returns {Signal} */
function parseSignal(text) {
  /** @type {unknown} */
  const value = JSON.parse(text);
  if (typeof value !== "object" || value === null || !("type" in value)) {
    throw new Error("Invalid server signal.");
  }
  switch (value.type) {
    case "offer":
      if ("sdp" in value && typeof value.sdp === "string") {
        return { type: "offer", sdp: value.sdp };
      }
      break;
    case "ice":
      if (
        "candidate" in value &&
        typeof value.candidate === "string" &&
        "sdpMLineIndex" in value &&
        typeof value.sdpMLineIndex === "number" &&
        Number.isInteger(value.sdpMLineIndex) &&
        value.sdpMLineIndex >= 0
      ) {
        return {
          type: "ice",
          candidate: value.candidate,
          sdpMLineIndex: value.sdpMLineIndex,
        };
      }
      break;
    case "status":
    case "error":
      if ("message" in value && typeof value.message === "string") {
        return { type: value.type, message: value.message };
      }
      break;
  }
  throw new Error("Invalid server signal.");
}

/** @param {Attempt} attempt @param {string} message */
function stopSession(attempt, message) {
  if (active !== attempt) return;
  active = null;
  const { peer, socket } = attempt;
  socket.onopen = socket.onmessage = socket.onerror = socket.onclose = null;
  peer.onicecandidate = peer.ontrack = peer.onconnectionstatechange = null;
  socket.close();
  peer.close();
  ui.video.srcObject = null;
  ui.start.disabled = false;
  ui.stop.disabled = true;
  ui.status.textContent = message;
}

/** @param {Attempt} attempt @param {unknown} error */
function fail(attempt, error) {
  stopSession(
    attempt,
    `Error: ${error instanceof Error ? error.message : String(error)}`,
  );
}

/** @param {Attempt} attempt @param {Signal} signal */
async function handleSignal(attempt, signal) {
  if (active !== attempt) return;
  const { peer, socket } = attempt;
  switch (signal.type) {
    case "offer": {
      if (peer.remoteDescription) throw new Error("Received a second offer.");
      await peer.setRemoteDescription({ type: "offer", sdp: signal.sdp });
      if (active !== attempt) return;
      for (const candidate of attempt.pending.splice(0)) {
        await peer.addIceCandidate(candidate);
        if (active !== attempt) return;
      }
      const answer = await peer.createAnswer();
      if (active !== attempt) return;
      await peer.setLocalDescription(answer);
      if (active !== attempt) return;
      if (!peer.localDescription) throw new Error("Missing local answer.");
      socket.send(
        JSON.stringify({ type: "answer", sdp: peer.localDescription.sdp }),
      );
      ui.status.textContent = "Answer sent; connecting media...";
      return;
    }
    case "ice": {
      const candidate = {
        candidate: signal.candidate,
        sdpMLineIndex: signal.sdpMLineIndex,
      };
      if (!peer.remoteDescription) {
        if (attempt.pending.length >= 128)
          throw new Error("Too many pending ICE candidates.");
        attempt.pending.push(candidate);
      } else {
        await peer.addIceCandidate(candidate);
      }
      return;
    }
    case "status":
      ui.status.textContent = signal.message;
      return;
    case "error":
      throw new Error(signal.message);
  }
}

start.addEventListener("click", () => {
  if (active) return;
  const peer = new RTCPeerConnection();
  const socket = new WebSocket(
    `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws`,
  );
  /** @type {Attempt} */
  const attempt = { peer, socket, pending: [], signals: Promise.resolve() };
  active = attempt;
  ui.start.disabled = true;
  ui.stop.disabled = false;
  ui.status.textContent = "Opening signaling socket...";

  peer.onicecandidate = (event) => {
    if (active !== attempt || !event.candidate) return;
    if (
      event.candidate.sdpMLineIndex === null ||
      socket.readyState !== WebSocket.OPEN
    ) {
      fail(
        attempt,
        new Error(
          "Cannot send an ICE candidate without an open signaling socket.",
        ),
      );
      return;
    }
    socket.send(
      JSON.stringify({
        type: "ice",
        sdpMLineIndex: event.candidate.sdpMLineIndex,
        candidate: event.candidate.candidate,
      }),
    );
  };
  peer.ontrack = (event) => {
    if (active !== attempt) return;
    ui.video.srcObject = event.streams[0] ?? new MediaStream([event.track]);
    ui.status.textContent = "Receiving video.";
  };
  peer.onconnectionstatechange = () => {
    if (["failed", "disconnected", "closed"].includes(peer.connectionState)) {
      stopSession(attempt, `Peer connection ${peer.connectionState}.`);
    }
  };
  socket.onopen = () => {
    if (active === attempt)
      ui.status.textContent = "Waiting for server offer...";
  };
  socket.onmessage = (event) => {
    if (active !== attempt) return;
    attempt.signals = attempt.signals
      .then(async () => {
        if (active !== attempt) return;
        if (typeof event.data !== "string")
          throw new Error("Expected a text server signal.");
        await handleSignal(attempt, parseSignal(event.data));
      })
      .catch((error) => fail(attempt, error));
  };
  socket.onerror = () =>
    stopSession(
      attempt,
      "WebSocket failed (the single viewer slot may be occupied).",
    );
  socket.onclose = () => stopSession(attempt, "Server disconnected.");
});

stop.addEventListener("click", () => {
  if (active) stopSession(active, "Stopped.");
});
