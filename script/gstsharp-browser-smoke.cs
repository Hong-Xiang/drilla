#:property TreatWarningsAsErrors=true
#:property TargetFramework=net10.0

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length is not (2 or 4))
{
    Console.Error.WriteLine(
        "Usage: dotnet run -p:ImportDirectoryPackagesProps=false script/gstsharp-browser-smoke.cs -- " +
        "<server-url> <chromium-debug-url> [expected-width expected-height]");
    return 2;
}

var server = new Uri(args[0]);
var debugger = new Uri(args[1]);
var expectedWidth = args.Length == 4 && int.TryParse(args[2], out var width) && width > 0
    ? width
    : args.Length == 2
        ? 320
        : throw new ArgumentException("Expected width must be a positive integer.");
var expectedHeight = args.Length == 4 && int.TryParse(args[3], out var height) && height > 0
    ? height
    : args.Length == 2
        ? 240
        : throw new ArgumentException("Expected height must be a positive integer.");
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
using var http = new HttpClient();
var cancellation = timeout.Token;
var previousFrame = 0L;

for (var iteration = 0; iteration < 4; iteration++)
{
    using var create = await http.PutAsync(new Uri(debugger, "/json/new?about:blank"), null, cancellation);
    create.EnsureSuccessStatusCode();
    using var target = JsonDocument.Parse(await create.Content.ReadAsStringAsync(cancellation));
    var targetId = target.RootElement.GetProperty("id").GetString()
        ?? throw new InvalidOperationException("Missing browser target ID.");
    var socketUrl = target.RootElement.GetProperty("webSocketDebuggerUrl").GetString()
        ?? throw new InvalidOperationException("Missing browser debugger URL.");
    try
    {
        await using var browser = new BrowserProtocol();
        await browser.ConnectAsync(new Uri(socketUrl), cancellation);
        await browser.CommandAsync("Page.enable", new JsonObject(), cancellation);
        await browser.CommandAsync("Page.addScriptToEvaluateOnNewDocument", new JsonObject
        {
            ["source"] = """
                globalThis.__mediaSockets = [];
                globalThis.__mediaStats = {
                  activeByPeer: new WeakMap(), peers: [], nativeGetStats: null,
                  holdNext: false, release: null, overlap: false
                };
                const NativeWebSocket = globalThis.WebSocket;
                globalThis.WebSocket = class extends NativeWebSocket {
                  constructor(url) { super(url); globalThis.__mediaSockets.push(this); }
                };
                const nativeGetStats = RTCPeerConnection.prototype.getStats;
                globalThis.__mediaStats.nativeGetStats = nativeGetStats;
                RTCPeerConnection.prototype.getStats = async function(...args) {
                  const state = globalThis.__mediaStats;
                  if (!state.peers.includes(this)) state.peers.push(this);
                  const active = (state.activeByPeer.get(this) ?? 0) + 1;
                  state.activeByPeer.set(this, active);
                  if (active > 1) state.overlap = true;
                  try {
                    if (state.holdNext) {
                      state.holdNext = false;
                      await new Promise(resolve => { state.release = resolve; });
                      state.release = null;
                    }
                    return await nativeGetStats.apply(this, args);
                  } finally {
                    state.activeByPeer.set(this, active - 1);
                  }
                };
                """
        }, cancellation);
        await browser.CommandAsync("Page.navigate", new JsonObject { ["url"] = server.AbsoluteUri }, cancellation);
        await browser.EvaluateAsync("""
            (async () => {
              for (let i = 0; i < 100; i++) {
                const start = document.getElementById('start');
                if (document.readyState === 'complete' && start) { start.click(); return true; }
                await new Promise(resolve => setTimeout(resolve, 100));
              }
              throw new Error('Viewer did not load');
            })()
            """, cancellation);

        var observation = await browser.EvaluateAsync("""
            (async () => {
              const video = document.getElementById('video');
              if (!(video instanceof HTMLVideoElement)) throw new Error('Missing video');
              const stats = document.getElementById('stats');
              if (!(stats instanceof HTMLElement)) throw new Error('Missing receiver stats');
              const canvas = document.createElement('canvas');
              const context = canvas.getContext('2d', {willReadFrequently: true});
              if (!context) throw new Error('Canvas unavailable');
              function sample(metadata) {
                canvas.width = Math.min(video.videoWidth, 320);
                canvas.height = Math.max(1, Math.round(
                  video.videoHeight * canvas.width / video.videoWidth));
                context.drawImage(video, 0, 0, canvas.width, canvas.height);
                const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
                let red = 0, green = 0, blue = 0, foreground = 0, background = 0;
                for (let i = 0; i < pixels.length; i += 4) {
                  const r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
                  const maximum = Math.max(r, g, b), minimum = Math.min(r, g, b);
                  if (maximum < 40) background++;
                  if (maximum > 90 && maximum - minimum > 45) foreground++;
                  if (r > 130 && r > g * 1.5 && r > b * 1.5) red++;
                  if (g > 130 && g > r * 1.5 && g > b * 1.5) green++;
                  if (b > 130 && b > r * 1.5 && b > g * 1.5) blue++;
                }
                return {pixels, metadata, red, green, blue, foreground, background};
              }
              function nextFrame() {
                return new Promise((resolve, reject) => {
                  const id = video.requestVideoFrameCallback((_, metadata) => {
                    clearTimeout(timer);
                    try { resolve(sample(metadata)); } catch (error) { reject(error); }
                  });
                  const timer = setTimeout(() => {
                    video.cancelVideoFrameCallback(id);
                    reject(new Error('Decoded video stopped advancing'));
                  }, 5000);
                });
              }
              for (let i = 0; i < 200; i++) {
                if (video.readyState >= 2) {
                  const first = await nextFrame();
                  await new Promise(resolve => setTimeout(resolve, 500));
                  const second = await nextFrame();
                  if (first.pixels.length !== second.pixels.length)
                    throw new Error('Video dimensions changed');
                  let difference = 0, motionPixels = 0;
                  for (let p = 0; p < first.pixels.length; p += 4) {
                    const delta = Math.abs(first.pixels[p] - second.pixels[p])
                      + Math.abs(first.pixels[p + 1] - second.pixels[p + 1])
                      + Math.abs(first.pixels[p + 2] - second.pixels[p + 2]);
                    difference += delta;
                    if (delta > 90) motionPixels++;
                  }
                  const motionScore = difference / (canvas.width * canvas.height * 3);
                  const decodedDelta = second.metadata.presentedFrames - first.metadata.presentedFrames;
                  const pixelCount = canvas.width * canvas.height;
                  if (decodedDelta <= 0 || second.metadata.mediaTime <= first.metadata.mediaTime)
                    throw new Error('Decoded frame metadata did not advance');
                  if (motionPixels < pixelCount * 0.01 || motionScore < 3)
                    throw new Error('Decoded GPU triangle did not rotate');
                  if (second.foreground < pixelCount * 0.05
                      || second.background < pixelCount * 0.5
                      || Math.min(second.red, second.green, second.blue) < pixelCount * 0.003)
                    throw new Error('Expected GPU triangle and dark background were not decoded');
                  for (let sample = 0; sample < 100; sample++) {
                    const mbps = Number(stats.dataset.mbps);
                    const fps = Number(stats.dataset.fps);
                    if (Number.isFinite(mbps) && mbps > 0
                        && Number.isFinite(fps) && fps > 0) {
                      return {
                        width: video.videoWidth, height: video.videoHeight,
                        decodedFrames: video.getVideoPlaybackQuality().totalVideoFrames,
                        decodedDelta, motionPixels, motionScore,
                        foreground: second.foreground, background: second.background,
                        red: second.red, green: second.green, blue: second.blue,
                        statsMbps: mbps, statsFps: fps
                      };
                    }
                    await new Promise(resolve => setTimeout(resolve, 100));
                  }
                  throw new Error('No finite receiver bitrate/frame-rate sample: '
                    + stats.textContent);
                }
                await new Promise(resolve => setTimeout(resolve, 100));
              }
              throw new Error('No decoded video: ' + document.getElementById('status')?.textContent);
            })()
            """, cancellation);
        if (observation.GetProperty("width").GetInt32() != expectedWidth
            || observation.GetProperty("height").GetInt32() != expectedHeight)
        {
            throw new InvalidOperationException($"Unexpected frame dimensions: {observation}");
        }
        if (observation.GetProperty("statsMbps").GetDouble() <= 0
            || observation.GetProperty("statsFps").GetDouble() <= 0)
        {
            throw new InvalidOperationException($"Receiver stats were not positive: {observation}");
        }
        var stable = await browser.EvaluateAsync("""
            (async () => {
              const state = globalThis.__mediaStats;
              const peer = state.peers.at(-1);
              if (!peer || typeof state.nativeGetStats !== 'function')
                throw new Error('Current receiver peer was not captured');
              async function sample() {
                const report = await state.nativeGetStats.call(peer);
                for (const value of report.values()) {
                  if (value.type === 'inbound-rtp' && value.kind === 'video') {
                    return {
                      timestamp: value.timestamp,
                      bytesReceived: value.bytesReceived,
                      framesDecoded: value.framesDecoded,
                      framesPerSecond: value.framesPerSecond,
                      packetsLost: value.packetsLost,
                      jitter: value.jitter
                    };
                  }
                }
                throw new Error('No inbound video RTP report');
              }
              const samples = [];
              for (let i = 0; i < 6; i++) {
                samples.push(await sample());
                if (i < 5) await new Promise(resolve => setTimeout(resolve, 1000));
              }
              const mbps = [], fps = [];
              for (let i = 1; i < samples.length; i++) {
                const previous = samples[i - 1], current = samples[i];
                const seconds = (current.timestamp - previous.timestamp) / 1000;
                if (!(seconds > 0)) throw new Error('Receiver stats time did not advance');
                mbps.push((current.bytesReceived - previous.bytesReceived) * 8 / seconds / 1e6);
                fps.push(
                  Number.isFinite(current.framesDecoded) && Number.isFinite(previous.framesDecoded)
                    ? (current.framesDecoded - previous.framesDecoded) / seconds
                    : current.framesPerSecond
                );
              }
              const finiteMbps = mbps.filter(Number.isFinite);
              const finiteFps = fps.filter(Number.isFinite);
              const jitters = samples.map(value => value.jitter).filter(Number.isFinite);
              const first = samples[0], last = samples.at(-1);
              return {
                seconds: (last.timestamp - first.timestamp) / 1000,
                mbpsCount: finiteMbps.length,
                mbpsAverage: finiteMbps.reduce((sum, value) => sum + value, 0) / finiteMbps.length,
                mbpsMin: Math.min(...finiteMbps), mbpsMax: Math.max(...finiteMbps),
                fpsCount: finiteFps.length,
                fpsAverage: finiteFps.reduce((sum, value) => sum + value, 0) / finiteFps.length,
                fpsMin: Math.min(...finiteFps), fpsMax: Math.max(...finiteFps),
                packetsLostStart: first.packetsLost, packetsLostEnd: last.packetsLost,
                packetsLostDelta: last.packetsLost - first.packetsLost,
                jitterMsAverage: jitters.reduce((sum, value) => sum + value, 0)
                  / jitters.length * 1000,
                jitterMsMax: Math.max(...jitters) * 1000
              };
            })()
            """, cancellation);
        if (stable.GetProperty("seconds").GetDouble() < 4
            || stable.GetProperty("mbpsCount").GetInt32() != 5
            || stable.GetProperty("mbpsMin").GetDouble() <= 0
            || stable.GetProperty("fpsCount").GetInt32() != 5
            || stable.GetProperty("fpsMin").GetDouble() <= 0)
        {
            throw new InvalidOperationException($"Stable receiver window was invalid: {stable}");
        }
        previousFrame = observation.GetProperty("decodedFrames").GetInt64();
        Console.WriteLine($"Connection {iteration + 1}: snapshot={observation} stable={stable}");

        if (iteration == 0)
        {
            using var extraViewer = new ClientWebSocket();
            extraViewer.Options.CollectHttpResponseDetails = true;
            var signaling = new UriBuilder(server) { Scheme = "ws", Path = "/ws" }.Uri;
            try
            {
                await extraViewer.ConnectAsync(signaling, cancellation);
                throw new InvalidOperationException("A concurrent second viewer was accepted.");
            }
            catch (WebSocketException) when (extraViewer.HttpStatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("Concurrent viewer rejected with HTTP 409.");
            }

            await browser.EvaluateAsync("""
                (async () => {
                  const oldSocket = globalThis.__mediaSockets.at(-1);
                  const oldClose = oldSocket.onclose, oldError = oldSocket.onerror;
                  if (!oldClose || !oldError) throw new Error('Missing stale callback fixture');
                  const video = document.getElementById('video');
                  const stats = document.getElementById('stats');
                  const statsState = globalThis.__mediaStats;
                  if (!(stats instanceof HTMLElement)) throw new Error('Missing receiver stats');
                  statsState.holdNext = true;
                  for (let i = 0; i < 50 && !statsState.release; i++)
                    await new Promise(resolve => setTimeout(resolve, 100));
                  if (!statsState.release) throw new Error('Could not hold an old stats request');
                  const oldStream = video.srcObject;
                  document.getElementById('stop').click();
                  if (stats.dataset.mbps !== undefined)
                    throw new Error('Stopping did not reset receiver stats');
                  await new Promise(resolve => setTimeout(resolve, 500));
                  document.getElementById('start').click();
                  oldClose.call(oldSocket, new CloseEvent('close'));
                  oldError.call(oldSocket, new Event('error'));
                  statsState.release();
                  function nextFrame() {
                    return new Promise((resolve, reject) => {
                      const id = video.requestVideoFrameCallback((_, metadata) => {
                        clearTimeout(timer);
                        resolve(metadata);
                      });
                      const timer = setTimeout(() => {
                        video.cancelVideoFrameCallback(id);
                        reject(new Error('Replacement video stopped advancing'));
                      }, 5000);
                    });
                  }
                  for (let i = 0; i < 200; i++) {
                    if (video.srcObject && video.srcObject !== oldStream && video.readyState >= 2) {
                      const replacement = video.srcObject;
                      const first = await nextFrame(), second = await nextFrame();
                      if (video.srcObject !== replacement || second.mediaTime <= first.mediaTime
                          || second.presentedFrames <= first.presentedFrames)
                        throw new Error('Replacement stream did not produce new frames');
                      for (let sample = 0; sample < 100; sample++) {
                        const mbps = Number(stats.dataset.mbps);
                        const fps = Number(stats.dataset.fps);
                        if (Number.isFinite(mbps) && mbps > 0
                            && Number.isFinite(fps) && fps > 0) {
                          if (statsState.overlap)
                            throw new Error('A connection overlapped getStats calls');
                          return true;
                        }
                        await new Promise(resolve => setTimeout(resolve, 100));
                      }
                      throw new Error('Replacement receiver stats never became measurable');
                    }
                    await new Promise(resolve => setTimeout(resolve, 100));
                  }
                  throw new Error('Old callbacks terminated the replacement session: '
                    + document.getElementById('status').textContent);
                })()
                """, cancellation);
            Console.WriteLine("Delayed old close/error callbacks did not terminate a replacement session.");
        }

        if (iteration != 2)
        {
            await browser.EvaluateAsync("""
                (async () => {
                  document.getElementById('stop').click();
                  for (let i = 0; i < 100; i++) {
                    if (document.getElementById('video').srcObject === null) return true;
                    await new Promise(resolve => setTimeout(resolve, 100));
                  }
                  throw new Error('Stop did not release video');
                })()
                """, cancellation);
        }
    }
    finally
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var close = await http.GetAsync(new Uri(debugger, $"/json/close/{targetId}"), cleanup.Token);
        close.EnsureSuccessStatusCode();
    }
    await Task.Delay(500, cancellation);
}

Console.WriteLine($"PASS: GPU triangle decoded and moved across four connections, including tab-close recovery; last count {previousFrame}.");
return 0;

sealed class BrowserProtocol : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private int nextId;

    public Task ConnectAsync(Uri uri, CancellationToken cancellation) =>
        socket.ConnectAsync(uri, cancellation);

    public async Task<JsonElement> CommandAsync(string method, JsonObject parameters, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var id = ++nextId;
        var command = Encoding.UTF8.GetBytes(new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters
        }.ToJsonString());
        await socket.SendAsync(command.AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
        var buffer = new byte[16384];
        while (true)
        {
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token);
                if (received.MessageType != WebSocketMessageType.Text)
                    throw new InvalidOperationException($"Unexpected debugger message: {received.MessageType}");
                message.Write(buffer, 0, received.Count);
                if (message.Length > 1024 * 1024)
                    throw new InvalidOperationException("Debugger message exceeds 1 MiB.");
            } while (!received.EndOfMessage);
            using var document = JsonDocument.Parse(message.ToArray());
            var response = document.RootElement;
            if (!response.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id)
                continue;
            if (response.TryGetProperty("error", out var error))
                throw new InvalidOperationException($"Browser protocol error: {error}");
            return response.GetProperty("result").Clone();
        }
    }

    public async Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellation)
    {
        var response = await CommandAsync("Runtime.evaluate",
            new JsonObject
            {
                ["expression"] = expression,
                ["awaitPromise"] = true,
                ["returnByValue"] = true
            }, cancellation);
        if (response.TryGetProperty("exceptionDetails", out var exception))
            throw new InvalidOperationException($"Browser script failed: {exception}");
        return response.GetProperty("result").GetProperty("value").Clone();
    }

    public ValueTask DisposeAsync()
    {
        socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
