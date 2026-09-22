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
var expectedWidth = args.Length == 4 && int.TryParse(args[2], out int width) && width > 0
    ? width
    : args.Length == 2
        ? 320
        : throw new ArgumentException("Expected width must be a positive integer.");
var expectedHeight = args.Length == 4 && int.TryParse(args[3], out int height) && height > 0
    ? height
    : args.Length == 2
        ? 240
        : throw new ArgumentException("Expected height must be a positive integer.");
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
using var http = new HttpClient();
CancellationToken cancellation = timeout.Token;

await using BrowserTarget first = await BrowserTarget.OpenAsync(
    http, debugger, server, cancellation);
await using BrowserTarget second = await BrowserTarget.OpenAsync(
    http, debugger, server, cancellation);
await first.StartAsync(cancellation);
await second.StartAsync(cancellation);

JsonElement[] initial =
[
    await first.EvaluateForegroundAsync(BrowserScripts.Observe, cancellation),
    await second.EvaluateForegroundAsync(BrowserScripts.Observe, cancellation),
];
foreach (JsonElement observation in initial)
{
    ValidateObservation(observation, expectedWidth, expectedHeight);
}

JsonElement[] stable =
[
    await first.EvaluateForegroundAsync(BrowserScripts.StableReceiver, cancellation),
    await second.EvaluateForegroundAsync(BrowserScripts.StableReceiver, cancellation),
];
foreach (JsonElement sample in stable)
{
    ValidateStable(sample);
}
Console.WriteLine($"Two simultaneous decoded animated streams: A={initial[0]} B={initial[1]}");

using (var extraViewer = new ClientWebSocket())
{
    extraViewer.Options.CollectHttpResponseDetails = true;
    var signaling = new UriBuilder(server) { Scheme = "ws", Path = "/ws" }.Uri;
    try
    {
        await extraViewer.ConnectAsync(signaling, cancellation);
        throw new InvalidOperationException("A third concurrent media session was accepted.");
    }
    catch (WebSocketException) when (extraViewer.HttpStatusCode == HttpStatusCode.Conflict)
    {
        Console.WriteLine("Third concurrent handshake rejected with HTTP 409.");
    }
}

JsonElement failed = await first.EvaluateForegroundAsync(
    BrowserScripts.FailMalformed, cancellation);
JsonElement survivingFailure = await second.EvaluateForegroundAsync(
    BrowserScripts.Progress, cancellation);
ValidateProgress(survivingFailure);
Console.WriteLine($"Malformed session stopped without stopping its peer: failed={failed} survivor={survivingFailure}");

JsonElement reconnected = await first.EvaluateAsync(BrowserScripts.Reconnect, cancellation);
ValidateObservation(reconnected, expectedWidth, expectedHeight);
JsonElement reconnectedStable = await first.EvaluateForegroundAsync(
    BrowserScripts.StableReceiver, cancellation);
ValidateStable(reconnectedStable);
JsonElement survivingReconnect = await second.EvaluateForegroundAsync(
    BrowserScripts.Progress, cancellation);
ValidateProgress(survivingReconnect);
Console.WriteLine($"Released slot reused by isolated reconnect: {reconnected}");

await second.DisposeAsync();
JsonElement survivingClose = await first.EvaluateForegroundAsync(
    BrowserScripts.Progress, cancellation);
ValidateProgress(survivingClose);
Console.WriteLine($"First stream survived abrupt peer tab close: {survivingClose}");

await using BrowserTarget replacement = await BrowserTarget.OpenAsync(
    http, debugger, server, cancellation);
await replacement.StartAsync(cancellation);
JsonElement[] reused =
[
    await replacement.EvaluateForegroundAsync(BrowserScripts.Observe, cancellation),
    await first.EvaluateForegroundAsync(BrowserScripts.Progress, cancellation),
];
ValidateObservation(reused[0], expectedWidth, expectedHeight);
ValidateProgress(reused[1]);
JsonElement invalid = await replacement.EvaluateForegroundAsync(
    BrowserScripts.FailInvalid, cancellation);
JsonElement survivingInvalid = await first.EvaluateForegroundAsync(
    BrowserScripts.Progress, cancellation);
ValidateProgress(survivingInvalid);
Console.WriteLine($"Invalid session stopped without stopping its peer: invalid={invalid} survivor={survivingInvalid}");

Console.WriteLine(
    "PASS: cap=2, concurrent decoded animation, HTTP 409, malformed/invalid/close isolation, " +
    "slot reuse, reconnect, and receiver stats.");
return 0;

static void ValidateObservation(JsonElement observation, int expectedWidth, int expectedHeight)
{
    if (observation.GetProperty("width").GetInt32() != expectedWidth ||
        observation.GetProperty("height").GetInt32() != expectedHeight)
    {
        throw new InvalidOperationException($"Unexpected frame dimensions: {observation}");
    }
    if (observation.GetProperty("statsMbps").GetDouble() <= 0 ||
        observation.GetProperty("statsFps").GetDouble() <= 0)
    {
        throw new InvalidOperationException($"Receiver stats were not positive: {observation}");
    }
}

static void ValidateStable(JsonElement stable)
{
    if (stable.GetProperty("seconds").GetDouble() < 1.5 ||
        stable.GetProperty("mbpsCount").GetInt32() != 4 ||
        stable.GetProperty("mbpsMin").GetDouble() <= 0 ||
        stable.GetProperty("fpsCount").GetInt32() != 4 ||
        stable.GetProperty("fpsMin").GetDouble() <= 0)
    {
        throw new InvalidOperationException($"Stable receiver window was invalid: {stable}");
    }
}

static void ValidateProgress(JsonElement progress)
{
    if (progress.GetProperty("presentedDelta").GetInt64() <= 0 ||
        progress.GetProperty("mediaDelta").GetDouble() <= 0 ||
        progress.GetProperty("statsMbps").GetDouble() <= 0 ||
        progress.GetProperty("statsFps").GetDouble() <= 0)
    {
        throw new InvalidOperationException($"Surviving stream did not progress: {progress}");
    }
}

static class BrowserScripts
{
    internal const string Instrument = """
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
        """;

    internal const string Start = """
        (async () => {
          for (let i = 0; i < 100; i++) {
            const start = document.getElementById('start');
            const video = document.getElementById('video');
            if (video instanceof HTMLVideoElement && video.srcObject) return true;
            if (document.readyState === 'complete' && start && !start.disabled) {
              start.click();
            }
            await new Promise(resolve => setTimeout(resolve, 100));
          }
          throw new Error('Viewer did not start: '
            + document.getElementById('status')?.textContent);
        })()
        """;

    internal const string Observe = """
        (async () => {
          const video = document.getElementById('video');
          const stats = document.getElementById('stats');
          if (!(video instanceof HTMLVideoElement)) throw new Error('Missing video');
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
        """;

    internal const string StableReceiver = """
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
                  framesPerSecond: value.framesPerSecond
                };
              }
            }
            throw new Error('No inbound video RTP report');
          }
          const samples = [];
          for (let i = 0; i < 5; i++) {
            samples.push(await sample());
            if (i < 4) await new Promise(resolve => setTimeout(resolve, 500));
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
          const first = samples[0], last = samples.at(-1);
          return {
            seconds: (last.timestamp - first.timestamp) / 1000,
            mbpsCount: finiteMbps.length,
            mbpsMin: Math.min(...finiteMbps),
            fpsCount: finiteFps.length,
            fpsMin: Math.min(...finiteFps)
          };
        })()
        """;

    internal const string Progress = """
        (async () => {
          const video = document.getElementById('video');
          const stats = document.getElementById('stats');
          if (!(video instanceof HTMLVideoElement) || !(stats instanceof HTMLElement))
            throw new Error('Missing viewer state');
          function nextFrame() {
            return new Promise((resolve, reject) => {
              const id = video.requestVideoFrameCallback((_, metadata) => {
                clearTimeout(timer);
                resolve(metadata);
              });
              const timer = setTimeout(() => {
                video.cancelVideoFrameCallback(id);
                reject(new Error('Surviving decoded video stopped'));
              }, 5000);
            });
          }
          const first = await nextFrame();
          await new Promise(resolve => setTimeout(resolve, 300));
          const second = await nextFrame();
          const mbps = Number(stats.dataset.mbps), fps = Number(stats.dataset.fps);
          return {
            presentedDelta: second.presentedFrames - first.presentedFrames,
            mediaDelta: second.mediaTime - first.mediaTime,
            statsMbps: mbps,
            statsFps: fps
          };
        })()
        """;

    internal const string FailMalformed = """
        (async () => {
          const socket = globalThis.__mediaSockets.at(-1);
          const video = document.getElementById('video');
          const statsState = globalThis.__mediaStats;
          if (!socket || !(video instanceof HTMLVideoElement))
            throw new Error('Missing active media session');
          globalThis.__staleMedia = {
            close: socket.onclose,
            error: socket.onerror,
            stream: video.srcObject
          };
          if (!globalThis.__staleMedia.close || !globalThis.__staleMedia.error)
            throw new Error('Missing stale callback fixture');
          statsState.holdNext = true;
          for (let i = 0; i < 50 && !statsState.release; i++)
            await new Promise(resolve => setTimeout(resolve, 100));
          if (!statsState.release) throw new Error('Could not hold an old stats request');
          socket.send('{');
          for (let i = 0; i < 100; i++) {
            if (video.srcObject === null)
              return {status: document.getElementById('status')?.textContent};
            await new Promise(resolve => setTimeout(resolve, 100));
          }
          throw new Error('Malformed signaling did not stop its session');
        })()
        """;

    internal const string FailInvalid = """
        (async () => {
          const socket = globalThis.__mediaSockets.at(-1);
          const video = document.getElementById('video');
          if (!socket || !(video instanceof HTMLVideoElement))
            throw new Error('Missing active media session');
          socket.send('{"type":"unsupported"}');
          for (let i = 0; i < 100; i++) {
            if (video.srcObject === null)
              return {status: document.getElementById('status')?.textContent};
            await new Promise(resolve => setTimeout(resolve, 100));
          }
          throw new Error('Invalid signaling did not stop its session');
        })()
        """;

    internal const string Reconnect = """
        (async () => {
          const video = document.getElementById('video');
          const stats = document.getElementById('stats');
          const stale = globalThis.__staleMedia;
          const statsState = globalThis.__mediaStats;
          if (!(video instanceof HTMLVideoElement) || !(stats instanceof HTMLElement) || !stale)
            throw new Error('Missing reconnect fixture');
          for (let i = 0; i < 200; i++) {
            const start = document.getElementById('start');
            if (!video.srcObject && start && !start.disabled) start.click();
            if (video.srcObject && video.srcObject !== stale.stream && video.readyState >= 2) {
              const replacement = video.srcObject;
              stale.close.call(globalThis.__mediaSockets.at(-2), new CloseEvent('close'));
              stale.error.call(globalThis.__mediaSockets.at(-2), new Event('error'));
              statsState.release();
              await new Promise(resolve => setTimeout(resolve, 100));
              for (let sample = 0; sample < 100; sample++) {
                if (video.srcObject !== replacement)
                  throw new Error('Old callbacks terminated the replacement session');
                const mbps = Number(stats.dataset.mbps);
                const fps = Number(stats.dataset.fps);
                if (Number.isFinite(mbps) && mbps > 0
                    && Number.isFinite(fps) && fps > 0) {
                  if (statsState.overlap)
                    throw new Error('A connection overlapped getStats calls');
                  return {
                    width: video.videoWidth, height: video.videoHeight,
                    statsMbps: mbps, statsFps: fps
                  };
                }
                await new Promise(resolve => setTimeout(resolve, 100));
              }
              throw new Error('Replacement receiver stats never became measurable');
            }
            await new Promise(resolve => setTimeout(resolve, 100));
          }
          throw new Error('Old callbacks terminated the replacement session: '
            + document.getElementById('status')?.textContent);
        })()
        """;
}

sealed class BrowserTarget : IAsyncDisposable
{
    private readonly HttpClient http;
    private readonly Uri debugger;
    private readonly string targetId;
    private readonly BrowserProtocol browser;
    private int disposed;

    private BrowserTarget(HttpClient http, Uri debugger, string targetId, BrowserProtocol browser)
    {
        this.http = http;
        this.debugger = debugger;
        this.targetId = targetId;
        this.browser = browser;
    }

    internal static async Task<BrowserTarget> OpenAsync(
        HttpClient http,
        Uri debugger,
        Uri server,
        CancellationToken cancellation)
    {
        using var create = await http.PutAsync(
            new Uri(debugger, "/json/new?about:blank"), null, cancellation);
        create.EnsureSuccessStatusCode();
        using var target = JsonDocument.Parse(await create.Content.ReadAsStringAsync(cancellation));
        string targetId = target.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Missing browser target ID.");
        string socketUrl = target.RootElement.GetProperty("webSocketDebuggerUrl").GetString()
            ?? throw new InvalidOperationException("Missing browser debugger URL.");
        var browser = new BrowserProtocol();
        try
        {
            await browser.ConnectAsync(new Uri(socketUrl), cancellation);
            await browser.CommandAsync("Page.enable", new JsonObject(), cancellation);
            await browser.CommandAsync(
                "Page.addScriptToEvaluateOnNewDocument",
                new JsonObject { ["source"] = BrowserScripts.Instrument },
                cancellation);
            await browser.CommandAsync(
                "Page.navigate",
                new JsonObject { ["url"] = server.AbsoluteUri },
                cancellation);
            return new(http, debugger, targetId, browser);
        }
        catch
        {
            await browser.DisposeAsync();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var close = await http.GetAsync(
                new Uri(debugger, $"/json/close/{targetId}"), cleanup.Token);
            throw;
        }
    }

    internal Task<JsonElement> StartAsync(CancellationToken cancellation) =>
        EvaluateAsync(BrowserScripts.Start, cancellation);

    internal Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellation) =>
        browser.EvaluateAsync(expression, cancellation);

    internal async Task<JsonElement> EvaluateForegroundAsync(
        string expression,
        CancellationToken cancellation)
    {
        await browser.CommandAsync("Page.bringToFront", new JsonObject(), cancellation);
        return await browser.EvaluateAsync(expression, cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        await browser.DisposeAsync();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var close = await http.GetAsync(
            new Uri(debugger, $"/json/close/{targetId}"), cleanup.Token);
        close.EnsureSuccessStatusCode();
    }
}

sealed class BrowserProtocol : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private int nextId;

    public Task ConnectAsync(Uri uri, CancellationToken cancellation) =>
        socket.ConnectAsync(uri, cancellation);

    public async Task<JsonElement> CommandAsync(
        string method,
        JsonObject parameters,
        CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        int id = ++nextId;
        byte[] command = Encoding.UTF8.GetBytes(new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        }.ToJsonString());
        await socket.SendAsync(
            command.AsMemory(), WebSocketMessageType.Text, endOfMessage: true, deadline.Token);
        var buffer = new byte[16384];
        while (true)
        {
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token);
                if (received.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidOperationException(
                        $"Unexpected debugger message: {received.MessageType}");
                }
                message.Write(buffer, 0, received.Count);
                if (message.Length > 1024 * 1024)
                {
                    throw new InvalidOperationException("Debugger message exceeds 1 MiB.");
                }
            }
            while (!received.EndOfMessage);
            using var document = JsonDocument.Parse(message.ToArray());
            JsonElement response = document.RootElement;
            if (!response.TryGetProperty("id", out JsonElement responseId) ||
                responseId.GetInt32() != id)
            {
                continue;
            }
            if (response.TryGetProperty("error", out JsonElement error))
            {
                throw new InvalidOperationException($"Browser protocol error: {error}");
            }
            return response.GetProperty("result").Clone();
        }
    }

    public async Task<JsonElement> EvaluateAsync(
        string expression,
        CancellationToken cancellation)
    {
        JsonElement response = await CommandAsync(
            "Runtime.evaluate",
            new JsonObject
            {
                ["expression"] = expression,
                ["awaitPromise"] = true,
                ["returnByValue"] = true,
            },
            cancellation);
        if (response.TryGetProperty("exceptionDetails", out JsonElement exception))
        {
            throw new InvalidOperationException($"Browser script failed: {exception}");
        }
        return response.GetProperty("result").GetProperty("value").Clone();
    }

    public ValueTask DisposeAsync()
    {
        socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
