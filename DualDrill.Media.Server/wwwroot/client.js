// @ts-check

/**
 * @typedef {{type: "offer", sdp: string}
 * | {type: "ice", candidate: string, sdpMLineIndex: number}
 * | {type: "status" | "error", message: string}} Signal
 * @typedef {{key: string, timestamp: number, bytesReceived: number,
 * framesDecoded: number | null, framesPerSecond: number | null,
 * packetsLost: number | null, jitter: number | null}} InboundSample
 * @typedef {{megabitsPerSecond: number, framesPerSecond: number | null}} Measurement
 * @typedef {{peer: RTCPeerConnection, socket: WebSocket,
 * pending: RTCIceCandidateInit[], signals: Promise<void>,
 * statsBaseline: InboundSample | null, statsTimer: number | null,
 * statsRunning: boolean}} Attempt
 */

const video = document.querySelector("video");
const status = document.querySelector("#status");
const stats = document.querySelector("#stats");
const start = document.querySelector("#start");
const stop = document.querySelector("#stop");
if (
  !(video instanceof HTMLVideoElement) ||
  !(status instanceof HTMLElement) ||
  !(stats instanceof HTMLElement) ||
  !(start instanceof HTMLButtonElement) ||
  !(stop instanceof HTMLButtonElement)
) {
  throw new Error("The viewer's required elements are missing.");
}
const ui = { video, status, stats, start, stop };
/** @type {Attempt | null} */
let active = null;

/** @param {unknown} value @param {string} name @returns {unknown} */
function field(value, name) {
  return typeof value === "object" && value !== null
    ? Reflect.get(value, name)
    : undefined;
}

/** @param {unknown} value @param {string} name */
function finiteNumber(value, name) {
  const number = field(value, name);
  return typeof number === "number" && Number.isFinite(number) ? number : null;
}

/** @param {unknown} value @returns {InboundSample | null} */
function parseInboundVideo(value) {
  const type = field(value, "type");
  const kind = field(value, "kind") ?? field(value, "mediaType");
  const id = field(value, "id");
  if (
    type !== "inbound-rtp" ||
    kind !== "video" ||
    typeof id !== "string"
  ) {
    return null;
  }
  const ssrc = finiteNumber(value, "ssrc");
  const timestamp = finiteNumber(value, "timestamp");
  const bytesReceived = finiteNumber(value, "bytesReceived");
  if (
    ssrc === null ||
    timestamp === null ||
    bytesReceived === null ||
    bytesReceived < 0
  ) {
    return null;
  }
  const framesDecoded = finiteNumber(value, "framesDecoded");
  const framesPerSecond = finiteNumber(value, "framesPerSecond");
  const packetsLost = finiteNumber(value, "packetsLost");
  const jitter = finiteNumber(value, "jitter");
  return {
    key: `${id}:${ssrc}`,
    timestamp,
    bytesReceived,
    framesDecoded:
      framesDecoded !== null && framesDecoded >= 0 ? framesDecoded : null,
    framesPerSecond:
      framesPerSecond !== null && framesPerSecond >= 0
        ? framesPerSecond
        : null,
    packetsLost,
    jitter: jitter !== null && jitter >= 0 ? jitter : null,
  };
}

/** @param {RTCStatsReport} report @returns {InboundSample | null} */
function findInboundVideo(report) {
  /** @type {InboundSample | null} */
  let found = null;
  report.forEach((value) => {
    found ??= parseInboundVideo(value);
  });
  return found;
}

/**
 * @param {InboundSample | null} previous
 * @param {InboundSample} current
 * @returns {Measurement | null}
 */
function calculateMeasurement(previous, current) {
  if (
    !previous ||
    previous.key !== current.key ||
    current.timestamp <= previous.timestamp ||
    current.bytesReceived < previous.bytesReceived ||
    (previous.framesDecoded !== null &&
      current.framesDecoded !== null &&
      current.framesDecoded < previous.framesDecoded)
  ) {
    return null;
  }
  const elapsedMilliseconds = current.timestamp - previous.timestamp;
  const decodedFrames =
    previous.framesDecoded !== null && current.framesDecoded !== null
      ? current.framesDecoded - previous.framesDecoded
      : null;
  const calculatedFramesPerSecond =
    decodedFrames === null
      ? null
      : (decodedFrames * 1000) / elapsedMilliseconds;
  return {
    megabitsPerSecond:
      ((current.bytesReceived - previous.bytesReceived) * 8) /
      elapsedMilliseconds /
      1000,
    framesPerSecond:
      current.framesPerSecond !== null && current.framesPerSecond > 0
        ? current.framesPerSecond
        : calculatedFramesPerSecond,
  };
}

function clearStats() {
  ui.stats.textContent = "Receiver stats: waiting for samples.";
  for (const name of ["width", "height", "fps", "mbps"]) {
    delete ui.stats.dataset[name];
  }
}

/**
 * @param {InboundSample} sample
 * @param {Measurement | null} measurement
 */
function showStats(sample, measurement) {
  const dimensions =
    ui.video.videoWidth > 0 && ui.video.videoHeight > 0
      ? `${ui.video.videoWidth}×${ui.video.videoHeight}`
      : "dimensions unavailable";
  if (!measurement) {
    clearStats();
    ui.stats.textContent = `Receiver stats: ${dimensions}; collecting rate sample…`;
    return;
  }
  const fps =
    measurement.framesPerSecond === null
      ? "decoded fps unavailable"
      : `${measurement.framesPerSecond.toFixed(1)} decoded fps`;
  const loss =
    sample.packetsLost === null
      ? "loss unavailable"
      : `${sample.packetsLost} packets lost`;
  const jitter =
    sample.jitter === null
      ? "jitter unavailable"
      : `${(sample.jitter * 1000).toFixed(1)} ms jitter`;
  ui.stats.textContent =
    `Receiver stats: ${dimensions}; ${fps}; ` +
    `${measurement.megabitsPerSecond.toFixed(2)} Mbps inbound RTP video; ` +
    `${loss}; ${jitter}.`;
  ui.stats.dataset.width = String(ui.video.videoWidth);
  ui.stats.dataset.height = String(ui.video.videoHeight);
  ui.stats.dataset.mbps = String(measurement.megabitsPerSecond);
  if (measurement.framesPerSecond !== null) {
    ui.stats.dataset.fps = String(measurement.framesPerSecond);
  } else {
    delete ui.stats.dataset.fps;
  }
}

/** @param {Attempt} attempt */
async function pollStats(attempt) {
  if (active !== attempt || !attempt.statsRunning) return;
  try {
    const report = await attempt.peer.getStats();
    if (active !== attempt || !attempt.statsRunning) return;
    const sample = findInboundVideo(report);
    if (!sample) {
      attempt.statsBaseline = null;
      clearStats();
    } else {
      const measurement = calculateMeasurement(attempt.statsBaseline, sample);
      attempt.statsBaseline = sample;
      showStats(sample, measurement);
    }
  } catch (error) {
    if (active !== attempt || !attempt.statsRunning) return;
    attempt.statsBaseline = null;
    clearStats();
    ui.stats.textContent = `Receiver stats unavailable: ${
      error instanceof Error ? error.message : String(error)
    }`;
  }
  if (active === attempt && attempt.statsRunning) {
    attempt.statsTimer = window.setTimeout(() => {
      attempt.statsTimer = null;
      void pollStats(attempt);
    }, 1000);
  }
}

/** @param {Attempt} attempt */
function startStats(attempt) {
  if (active !== attempt || attempt.statsRunning) return;
  attempt.statsRunning = true;
  void pollStats(attempt);
}

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
  attempt.statsRunning = false;
  attempt.statsBaseline = null;
  if (attempt.statsTimer !== null) {
    clearTimeout(attempt.statsTimer);
    attempt.statsTimer = null;
  }
  socket.onopen = socket.onmessage = socket.onerror = socket.onclose = null;
  peer.onicecandidate = peer.ontrack = peer.onconnectionstatechange = null;
  socket.close();
  peer.close();
  ui.video.srcObject = null;
  ui.start.disabled = false;
  ui.stop.disabled = true;
  ui.status.textContent = message;
  clearStats();
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
  const attempt = {
    peer,
    socket,
    pending: [],
    signals: Promise.resolve(),
    statsBaseline: null,
    statsTimer: null,
    statsRunning: false,
  };
  active = attempt;
  ui.start.disabled = true;
  ui.stop.disabled = false;
  ui.status.textContent = "Opening signaling socket...";
  clearStats();

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
    startStats(attempt);
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
      "WebSocket failed (the media session limit may be reached).",
    );
  socket.onclose = () => stopSession(attempt, "Server disconnected.");
});

stop.addEventListener("click", () => {
  if (active) stopSession(active, "Stopped.");
});
