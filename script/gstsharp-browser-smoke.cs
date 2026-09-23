#:property TreatWarningsAsErrors=true
#:property TargetFramework=net10.0

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

bool raymarch = args.Length is 3 or 5 && args[2] == "--raymarch";
if (args.Length is not (2 or 4) && !raymarch)
{
    Console.Error.WriteLine(
        "Usage: dotnet run -p:ImportDirectoryPackagesProps=false script/gstsharp-browser-smoke.cs -- " +
        "<server-url> <chromium-debug-url> [expected-width expected-height]\n" +
        "   or: dotnet run -p:ImportDirectoryPackagesProps=false script/gstsharp-browser-smoke.cs -- " +
        "<server-url> <chromium-debug-url> --raymarch [expected-width expected-height]");
    return 2;
}

var server = new Uri(args[0]);
var debugger = new Uri(args[1]);
int dimensionsOffset = raymarch ? 3 : 2;
var expectedWidth = args.Length == dimensionsOffset + 2 &&
    int.TryParse(args[dimensionsOffset], out int width) && width > 0
    ? width
    : args.Length == dimensionsOffset
        ? 320
        : throw new ArgumentException("Expected width must be a positive integer.");
var expectedHeight = args.Length == dimensionsOffset + 2 &&
    int.TryParse(args[dimensionsOffset + 1], out int height) && height > 0
    ? height
    : args.Length == dimensionsOffset
        ? 240
        : throw new ArgumentException("Expected height must be a positive integer.");
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
using var http = new HttpClient();
CancellationToken cancellation = timeout.Token;

if (raymarch)
{
    return await RunRaymarchAcceptanceAsync(
        http,
        debugger,
        server,
        expectedWidth,
        expectedHeight,
        cancellation);
}

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

JsonElement[] initialPositions =
[
    await first.EvaluateForegroundAsync(BrowserScripts.Position, cancellation),
    await second.EvaluateForegroundAsync(BrowserScripts.Position, cancellation),
];
await first.DragMouseAsync(1.05, 0.2, cancellation);
JsonElement[] afterMouse =
[
    await first.EvaluateForegroundAsync(BrowserScripts.Position, cancellation),
    await second.EvaluateForegroundAsync(BrowserScripts.Position, cancellation),
];
ValidateMoved(initialPositions[0], afterMouse[0], 0.2);
ValidateRightEdge(afterMouse[0]);
ValidateNear(initialPositions[1], afterMouse[1], 0.08);
Console.WriteLine($"Captured mouse release moved only A: A={afterMouse[0]} B={afterMouse[1]}");

await second.DragTouchAsync(0.15, 0.75, cancellation);
JsonElement[] afterTouch =
[
    await first.EvaluateForegroundAsync(BrowserScripts.Position, cancellation),
    await second.EvaluateForegroundAsync(BrowserScripts.Position, cancellation),
];
ValidateNear(afterMouse[0], afterTouch[0], 0.12);
ValidateMoved(afterMouse[1], afterTouch[1], 0.2);
Console.WriteLine($"Touch drag moved only B: A={afterTouch[0]} B={afterTouch[1]}");

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

await first.EvaluateForegroundAsync(BrowserScripts.HoldPointerFrame, cancellation);
await first.DragMouseAsync(0.25, 0.25, cancellation);
JsonElement failed = await first.EvaluateForegroundAsync(
    BrowserScripts.FailMalformed, cancellation);
JsonElement survivingFailure = await second.EvaluateForegroundAsync(
    BrowserScripts.Progress, cancellation);
ValidateProgress(survivingFailure);
JsonElement survivingPosition = await second.EvaluateForegroundAsync(
    BrowserScripts.Position, cancellation);
ValidateNear(afterTouch[1], survivingPosition, 0.08);
Console.WriteLine($"Malformed session stopped without stopping its peer: failed={failed} survivor={survivingFailure}");

JsonElement reconnected = await first.EvaluateAsync(BrowserScripts.Reconnect, cancellation);
ValidateObservation(reconnected, expectedWidth, expectedHeight);
if (!reconnected.GetProperty("stalePointerCancelled").GetBoolean() ||
    reconnected.GetProperty("replacementPointerMessages").GetInt32() != 0)
{
    throw new InvalidOperationException($"Stale pointer work reached the replacement: {reconnected}");
}
JsonElement reconnectedStable = await first.EvaluateForegroundAsync(
    BrowserScripts.StableReceiver, cancellation);
ValidateStable(reconnectedStable);
JsonElement resetPosition = await first.EvaluateForegroundAsync(
    BrowserScripts.Position, cancellation);
ValidateNear(initialPositions[0], resetPosition, 0.08);
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
    "PASS: cap=2, concurrent decoded animation, mouse/touch input isolation, HTTP 409, " +
    "malformed/invalid/close isolation, slot reuse, reconnect reset, stale input teardown, " +
    "and receiver stats.");
return 0;

static async Task<int> RunRaymarchAcceptanceAsync(
    HttpClient http,
    Uri debugger,
    Uri server,
    int expectedWidth,
    int expectedHeight,
    CancellationToken cancellation)
{
    await using BrowserTarget first = await BrowserTarget.OpenAsync(
        http, debugger, server, cancellation);
    await using BrowserTarget second = await BrowserTarget.OpenAsync(
        http, debugger, server, cancellation);
    await second.SelectSceneAsync(BrowserScene.Raymarching, cancellation);
    await first.StartAsync(cancellation);
    await second.StartAsync(cancellation);

    JsonElement triangle = await first.EvaluateForegroundAsync(
        BrowserScripts.Observe, cancellation);
    JsonElement triangleControl = await first.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    if (HasRaymarchCoverage(triangleControl))
    {
        throw new InvalidOperationException("Raymarch classification accepted the triangle negative control.");
    }
    JsonElement raymarch = await second.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    ValidateObservation(triangle, expectedWidth, expectedHeight);
    ValidateRaymarch(raymarch, expectedWidth, expectedHeight);
    await ValidateActiveSceneAsync(first, BrowserScene.Triangle, cancellation);
    await ValidateActiveSceneAsync(second, BrowserScene.Raymarching, cancellation);
    ValidateStable(await first.EvaluateForegroundAsync(
        BrowserScripts.StableReceiver, cancellation));
    ValidateStable(await second.EvaluateForegroundAsync(
        BrowserScripts.StableReceiver, cancellation));
    Console.WriteLine(
        $"Mixed scene choice: triangle={triangle} " +
        $"triangleDarkPixels={triangleControl.GetProperty("darkPixels")} " +
        $"raymarch={RaymarchMetrics(raymarch)}");

    foreach (string query in new[]
    {
        "?scene=",
        "?scene=unknown",
        "?scene=triangle&scene=raymarching",
        "?scene=triangle&extra=value",
    })
    {
        await ExpectHandshakeStatusAsync(
            server,
            query,
            HttpStatusCode.BadRequest,
            cancellation);
    }
    await ExpectHandshakeStatusAsync(server, "", HttpStatusCode.Conflict, cancellation);
    Console.WriteLine("Invalid scene queries rejected with HTTP 400 before the full admission gate.");

    await first.EvaluateForegroundAsync(BrowserScripts.HoldPointerFrame, cancellation);
    await first.DragMouseAsync(0.25, 0.25, cancellation);
    JsonElement failed = await first.EvaluateForegroundAsync(
        BrowserScripts.FailMalformed, cancellation);
    ValidateProgress(await second.EvaluateForegroundAsync(
        BrowserScripts.Progress, cancellation));

    await first.SelectSceneAsync(BrowserScene.Raymarching, cancellation);
    JsonElement reconnected = await first.EvaluateAsync(
        BrowserScripts.Reconnect, cancellation);
    ValidateObservation(reconnected, expectedWidth, expectedHeight);
    if (!reconnected.GetProperty("stalePointerCancelled").GetBoolean() ||
        reconnected.GetProperty("replacementPointerMessages").GetInt32() != 0)
    {
        throw new InvalidOperationException($"Stale pointer work reached the replacement: {reconnected}");
    }
    await ValidateActiveSceneAsync(first, BrowserScene.Raymarching, cancellation);
    JsonElement firstRaymarch = await first.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    ValidateRaymarch(firstRaymarch, expectedWidth, expectedHeight);
    Console.WriteLine(
        $"Triangle stopped and reconnected as raymarch while its peer survived: " +
        $"failed={failed} replacement={RaymarchMetrics(firstRaymarch)}");

    JsonElement secondBeforeMouse = await second.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    JsonElement firstBeforeMouse = await first.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    int firstPointerCount = await first.PointerMessageCountAsync(cancellation);
    int secondPointerCount = await second.PointerMessageCountAsync(cancellation);
    await first.DragMouseAsync(0.95, 0.2, cancellation);
    await first.WaitForPointerMessageAsync(firstPointerCount, cancellation);
    await Task.Delay(100, cancellation);
    JsonElement firstAfterMouse = await first.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    JsonElement secondAfterMouse = await second.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    ValidateRaymarchInput(
        firstBeforeMouse,
        firstAfterMouse,
        "mouse",
        expectInputChange: true);
    ValidateRaymarchInput(
        secondBeforeMouse,
        secondAfterMouse,
        "mouse peer",
        expectInputChange: false);
    if (await first.PointerMessageCountAsync(cancellation) <= firstPointerCount ||
        await second.PointerMessageCountAsync(cancellation) != secondPointerCount)
    {
        throw new InvalidOperationException("Mouse input crossed raymarch browser sessions.");
    }

    JsonElement firstBeforeTouch = await first.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    JsonElement secondBeforeTouch = await second.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    firstPointerCount = await first.PointerMessageCountAsync(cancellation);
    secondPointerCount = await second.PointerMessageCountAsync(cancellation);
    await second.DragTouchAsync(0.05, 0.8, cancellation);
    await second.WaitForPointerMessageAsync(secondPointerCount, cancellation);
    await Task.Delay(100, cancellation);
    JsonElement secondAfterTouch = await second.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    JsonElement firstAfterTouch = await first.EvaluateForegroundAsync(
        BrowserScripts.RaymarchFrames, cancellation);
    ValidateRaymarchInput(
        secondBeforeTouch,
        secondAfterTouch,
        "touch",
        expectInputChange: true);
    ValidateRaymarchInput(
        firstBeforeTouch,
        firstAfterTouch,
        "touch peer",
        expectInputChange: false);
    if (await second.PointerMessageCountAsync(cancellation) <= secondPointerCount ||
        await first.PointerMessageCountAsync(cancellation) != firstPointerCount)
    {
        throw new InvalidOperationException("Touch input crossed raymarch browser sessions.");
    }
    Console.WriteLine(
        $"Raymarch input exceeded natural drift without peer input: " +
        $"mouse={RaymarchComparison(firstBeforeMouse, firstAfterMouse)} " +
        $"mousePeer={RaymarchComparison(secondBeforeMouse, secondAfterMouse)} " +
        $"touch={RaymarchComparison(secondBeforeTouch, secondAfterTouch)} " +
        $"touchPeer={RaymarchComparison(firstBeforeTouch, firstAfterTouch)}");

    await second.DisposeAsync();
    JsonElement survivingClose = await first.EvaluateForegroundAsync(
        BrowserScripts.Progress, cancellation);
    ValidateProgress(survivingClose);

    await using BrowserTarget replacement = await BrowserTarget.OpenAsync(
        http, debugger, server, cancellation);
    await replacement.SelectSceneAsync(BrowserScene.Raymarching, cancellation);
    await replacement.StartAsync(cancellation);
    ValidateRaymarch(
        await replacement.EvaluateForegroundAsync(BrowserScripts.RaymarchFrames, cancellation),
        expectedWidth,
        expectedHeight);
    JsonElement invalid = await replacement.EvaluateForegroundAsync(
        BrowserScripts.FailInvalid, cancellation);
    JsonElement survivingInvalid = await first.EvaluateForegroundAsync(
        BrowserScripts.Progress, cancellation);
    ValidateProgress(survivingInvalid);
    Console.WriteLine(
        $"Disconnect and invalid signaling stayed contained: close={survivingClose} " +
        $"invalid={invalid} survivor={survivingInvalid}");

    Console.WriteLine(
        "PASS RAYMARCH: mixed fixed scenes, two-raymarch reconnect, temporal-drift-aware " +
        "mouse/touch isolation, pre-admission HTTP 400, stale input teardown, disconnect/" +
        "invalid containment, shared receiver stats, and cap=2.");
    return 0;
}

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

static void ValidateRaymarch(JsonElement observation, int expectedWidth, int expectedHeight)
{
    ValidateObservation(observation, expectedWidth, expectedHeight);
    if (!HasRaymarchCoverage(observation) ||
        observation.GetProperty("luminanceRange").GetInt32() < 40 ||
        observation.GetProperty("varyingPixels").GetInt32() <
        observation.GetProperty("samplePixels").GetInt32() / 4 ||
        observation.GetProperty("naturalDrift").GetDouble() <= 0.2)
    {
        throw new InvalidOperationException($"Decoded raymarch structure was invalid: {observation}");
    }
}

// The canonical scene fills the frame; the diagnostic triangle leaves a large near-black background.
static bool HasRaymarchCoverage(JsonElement observation) =>
    observation.GetProperty("darkPixels").GetInt32() <
    observation.GetProperty("samplePixels").GetInt32() / 5;

static void ValidateRaymarchInput(
    JsonElement before,
    JsonElement after,
    string input,
    bool expectInputChange)
{
    if (!HasRaymarchCoverage(before) || !HasRaymarchCoverage(after))
    {
        throw new InvalidOperationException($"Raymarch {input} produced a triangle-like background.");
    }
    double comparison = FrameDifference(
        before.GetProperty("second"),
        after.GetProperty("first"));
    double expectedNatural = ExpectedNaturalDifference(before, after);

    if (expectInputChange
        ? comparison <= expectedNatural * 1.25 + 1
        : comparison > expectedNatural * 2 + 3)
    {
        double seconds =
            after.GetProperty("firstMediaTime").GetDouble() -
            before.GetProperty("secondMediaTime").GetDouble();
        throw new InvalidOperationException(
            $"Raymarch {input} comparison did not match temporal drift: " +
            $"difference={comparison:F2}, expectedNatural={expectedNatural:F2}, " +
            $"elapsed={seconds:F3}s.");
    }
}

static double ExpectedNaturalDifference(JsonElement before, JsonElement after)
{
    double seconds =
        after.GetProperty("firstMediaTime").GetDouble() -
        before.GetProperty("secondMediaTime").GetDouble();
    double naturalRate = Math.Max(
        NaturalDriftRate(before),
        NaturalDriftRate(after));
    return naturalRate * Math.Max(seconds, 1.0 / 30);
}

static double NaturalDriftRate(JsonElement sample)
{
    double seconds =
        sample.GetProperty("secondMediaTime").GetDouble() -
        sample.GetProperty("firstMediaTime").GetDouble();
    return sample.GetProperty("naturalDrift").GetDouble() / Math.Max(seconds, 0.001);
}

static double FrameDifference(JsonElement first, JsonElement second)
{
    if (first.GetArrayLength() != second.GetArrayLength() ||
        first.GetArrayLength() == 0)
    {
        throw new InvalidOperationException("Raymarch frame samples had different shapes.");
    }
    JsonElement.ArrayEnumerator left = first.EnumerateArray();
    JsonElement.ArrayEnumerator right = second.EnumerateArray();
    long total = 0;
    int channels = 0;
    while (left.MoveNext() && right.MoveNext())
    {
        int a = left.Current.GetInt32();
        int b = right.Current.GetInt32();
        total += Math.Abs((a >> 16) - (b >> 16));
        total += Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255));
        total += Math.Abs((a & 255) - (b & 255));
        channels += 3;
    }
    return (double)total / channels;
}

static string RaymarchMetrics(JsonElement sample) =>
    $"{{range={sample.GetProperty("luminanceRange")}, " +
    $"dark={sample.GetProperty("darkPixels")}/{sample.GetProperty("samplePixels")}, " +
    $"varying={sample.GetProperty("varyingPixels")}, " +
    $"drift={sample.GetProperty("naturalDrift").GetDouble():F2}, " +
    $"mbps={sample.GetProperty("statsMbps").GetDouble():F2}, " +
    $"fps={sample.GetProperty("statsFps").GetDouble():F2}}}";

static string RaymarchComparison(JsonElement before, JsonElement after) =>
    $"{{difference={FrameDifference(before.GetProperty("second"), after.GetProperty("first")):F2}, " +
    $"expectedNatural={ExpectedNaturalDifference(before, after):F2}, " +
    $"naturalBefore={before.GetProperty("naturalDrift").GetDouble():F2}, " +
    $"naturalAfter={after.GetProperty("naturalDrift").GetDouble():F2}}}";

static async Task ValidateActiveSceneAsync(
    BrowserTarget target,
    BrowserScene expected,
    CancellationToken cancellation)
{
    JsonElement state = await target.EvaluateForegroundAsync(
        BrowserScripts.SceneState, cancellation);
    string value = expected == BrowserScene.Triangle ? "triangle" : "raymarching";
    if (state.GetProperty("value").GetString() != value ||
        !state.GetProperty("disabled").GetBoolean())
    {
        throw new InvalidOperationException($"Active scene selector was mutable or wrong: {state}");
    }
}

static async Task ExpectHandshakeStatusAsync(
    Uri server,
    string query,
    HttpStatusCode expected,
    CancellationToken cancellation)
{
    using var socket = new ClientWebSocket();
    socket.Options.CollectHttpResponseDetails = true;
    var signaling = new UriBuilder(server)
    {
        Scheme = server.Scheme == "https" ? "wss" : "ws",
        Path = "/ws",
        Query = query.TrimStart('?'),
    }.Uri;
    try
    {
        await socket.ConnectAsync(signaling, cancellation);
        throw new InvalidOperationException($"Unexpectedly accepted signaling URI {signaling}.");
    }
    catch (WebSocketException) when (socket.HttpStatusCode == expected)
    {
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

static void ValidateMoved(JsonElement before, JsonElement after, double minimumDistance)
{
    double x = after.GetProperty("x").GetDouble() - before.GetProperty("x").GetDouble();
    double y = after.GetProperty("y").GetDouble() - before.GetProperty("y").GetDouble();
    if (Math.Sqrt(x * x + y * y) < minimumDistance)
    {
        throw new InvalidOperationException($"Decoded triangle position did not move enough: {before} -> {after}");
    }
}

static void ValidateNear(JsonElement expected, JsonElement actual, double maximumDistance)
{
    double x = actual.GetProperty("x").GetDouble() - expected.GetProperty("x").GetDouble();
    double y = actual.GetProperty("y").GetDouble() - expected.GetProperty("y").GetDouble();
    if (Math.Sqrt(x * x + y * y) > maximumDistance)
    {
        throw new InvalidOperationException($"Decoded triangle position changed unexpectedly: {expected} -> {actual}");
    }
}

static void ValidateRightEdge(JsonElement position)
{
    if (position.GetProperty("x").GetDouble() < 0.75)
    {
        throw new InvalidOperationException($"The final captured mouse-up position was lost: {position}");
    }
}

static class BrowserScripts
{
    internal const string Instrument = """
        globalThis.__mediaSockets = [];
        globalThis.__mediaSent = new WeakMap();
        globalThis.__mediaStats = {
          activeByPeer: new WeakMap(), peers: [], nativeGetStats: null,
          holdNext: false, release: null, overlap: false
        };
        globalThis.__mediaAnimation = {holdNext: false, held: null};
        const NativeWebSocket = globalThis.WebSocket;
        globalThis.WebSocket = class extends NativeWebSocket {
          constructor(url) {
            super(url);
            globalThis.__mediaSockets.push(this);
            globalThis.__mediaSent.set(this, []);
          }
          send(data) {
            globalThis.__mediaSent.get(this).push(data);
            super.send(data);
          }
        };
        const nativeAnimationFrame = globalThis.requestAnimationFrame;
        const nativeCancelAnimationFrame = globalThis.cancelAnimationFrame;
        globalThis.requestAnimationFrame = callback => {
          const state = globalThis.__mediaAnimation;
          if (!state.holdNext) return nativeAnimationFrame.call(globalThis, callback);
          state.holdNext = false;
          const id = nativeAnimationFrame.call(globalThis, () => {});
          state.held = {id, callback, cancelled: false};
          return id;
        };
        globalThis.cancelAnimationFrame = id => {
          const held = globalThis.__mediaAnimation.held;
          if (held?.id === id) held.cancelled = true;
          nativeCancelAnimationFrame.call(globalThis, id);
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

    internal const string SceneState = """
        (() => {
          const scene = document.getElementById('scene');
          if (!(scene instanceof HTMLSelectElement)) throw new Error('Missing scene selector');
          return {value: scene.value, disabled: scene.disabled};
        })()
        """;

    internal const string Position = """
        (async () => {
          const video = document.getElementById('video');
          if (!(video instanceof HTMLVideoElement)) throw new Error('Missing video');
          const canvas = document.createElement('canvas');
          const context = canvas.getContext('2d', {willReadFrequently: true});
          if (!context) throw new Error('Canvas unavailable');
          canvas.width = Math.min(video.videoWidth, 320);
          canvas.height = Math.max(1, Math.round(
            video.videoHeight * canvas.width / video.videoWidth));
          function nextFrame() {
            return new Promise((resolve, reject) => {
              const id = video.requestVideoFrameCallback(() => {
                clearTimeout(timer);
                context.drawImage(video, 0, 0, canvas.width, canvas.height);
                const pixels = context.getImageData(
                  0, 0, canvas.width, canvas.height).data;
                let count = 0, xTotal = 0, yTotal = 0;
                for (let y = 0; y < canvas.height; y++) {
                  for (let x = 0; x < canvas.width; x++) {
                    const i = (y * canvas.width + x) * 4;
                    const r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
                    const maximum = Math.max(r, g, b);
                    const minimum = Math.min(r, g, b);
                    if (maximum > 80 && maximum - minimum > 35) {
                      count++;
                      xTotal += x;
                      yTotal += y;
                    }
                  }
                }
                if (count < canvas.width * canvas.height * 0.005)
                  reject(new Error('Decoded triangle foreground was too small'));
                else
                  resolve({
                    x: xTotal / count / canvas.width,
                    y: yTotal / count / canvas.height,
                    foreground: count
                  });
              });
              const timer = setTimeout(() => {
                video.cancelVideoFrameCallback(id);
                reject(new Error('Decoded position frame timed out'));
              }, 5000);
            });
          }
          const positions = [];
          for (let i = 0; i < 4; i++) positions.push(await nextFrame());
          return {
            x: positions.reduce((sum, value) => sum + value.x, 0) / positions.length,
            y: positions.reduce((sum, value) => sum + value.y, 0) / positions.length,
            foreground: Math.min(...positions.map(value => value.foreground))
          };
        })()
        """;

    internal const string HoldPointerFrame = """
        (() => {
          globalThis.__mediaAnimation.holdNext = true;
          globalThis.__mediaAnimation.held = null;
          return true;
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

    internal const string RaymarchFrames = """
        (async () => {
          const video = document.getElementById('video');
          const stats = document.getElementById('stats');
          if (!(video instanceof HTMLVideoElement)) throw new Error('Missing video');
          if (!(stats instanceof HTMLElement)) throw new Error('Missing receiver stats');
          const canvas = document.createElement('canvas');
          const context = canvas.getContext('2d', {willReadFrequently: true});
          if (!context) throw new Error('Canvas unavailable');
          function capture() {
            return new Promise((resolve, reject) => {
              const id = video.requestVideoFrameCallback((_, metadata) => {
                clearTimeout(timer);
                try {
                  canvas.width = Math.min(video.videoWidth, 80);
                  canvas.height = Math.max(1, Math.round(
                    video.videoHeight * canvas.width / video.videoWidth));
                  context.drawImage(video, 0, 0, canvas.width, canvas.height);
                  const rgba = context.getImageData(
                    0, 0, canvas.width, canvas.height).data;
                  const pixels = [], luminance = [];
                  let minimum = 255, maximum = 0, total = 0, dark = 0;
                  for (let i = 0; i < rgba.length; i += 4) {
                    const r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
                    if (Math.max(r, g, b) <= 24) dark++;
                    pixels.push((r << 16) | (g << 8) | b);
                    const value = Math.round((r * 3 + g * 6 + b) / 10);
                    luminance.push(value);
                    minimum = Math.min(minimum, value);
                    maximum = Math.max(maximum, value);
                    total += value;
                  }
                  const mean = total / luminance.length;
                  resolve({
                    pixels,
                    mediaTime: metadata.mediaTime,
                    darkPixels: dark,
                    luminanceRange: maximum - minimum,
                    varyingPixels: luminance.filter(value => Math.abs(value - mean) > 12).length
                  });
                } catch (error) {
                  reject(error);
                }
              });
              const timer = setTimeout(() => {
                video.cancelVideoFrameCallback(id);
                reject(new Error('Decoded raymarch frame timed out'));
              }, 5000);
            });
          }
          function difference(first, second) {
            let total = 0;
            for (let i = 0; i < first.length; i++) {
              const a = first[i], b = second[i];
              total += Math.abs((a >> 16) - (b >> 16));
              total += Math.abs(((a >> 8) & 255) - ((b >> 8) & 255));
              total += Math.abs((a & 255) - (b & 255));
            }
            return total / first.length / 3;
          }
          for (let attempt = 0; attempt < 200; attempt++) {
            if (video.readyState >= 2 && video.videoWidth > 0) {
              const first = await capture();
              await new Promise(resolve => setTimeout(resolve, 250));
              const second = await capture();
              for (let sample = 0; sample < 100; sample++) {
                const mbps = Number(stats.dataset.mbps);
                const fps = Number(stats.dataset.fps);
                if (Number.isFinite(mbps) && mbps > 0
                    && Number.isFinite(fps) && fps > 0) {
                  return {
                    width: video.videoWidth,
                    height: video.videoHeight,
                    samplePixels: second.pixels.length,
                    luminanceRange: second.luminanceRange,
                    varyingPixels: second.varyingPixels,
                    darkPixels: second.darkPixels,
                    naturalDrift: difference(first.pixels, second.pixels),
                    firstMediaTime: first.mediaTime,
                    secondMediaTime: second.mediaTime,
                    first: first.pixels,
                    second: second.pixels,
                    statsMbps: mbps,
                    statsFps: fps
                  };
                }
                await new Promise(resolve => setTimeout(resolve, 100));
              }
              throw new Error('No finite raymarch receiver statistics: ' + stats.textContent);
            }
            await new Promise(resolve => setTimeout(resolve, 100));
          }
          throw new Error('No decoded raymarch video: '
            + document.getElementById('status')?.textContent);
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
          socket.send('{"type":"pointer","x":2,"y":0.5}');
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
          const animation = globalThis.__mediaAnimation;
          if (!(video instanceof HTMLVideoElement) || !(stats instanceof HTMLElement)
              || !stale || !animation.held)
            throw new Error('Missing reconnect fixture');
          for (let i = 0; i < 200; i++) {
            const start = document.getElementById('start');
            const scene = document.getElementById('scene');
            if (!video.srcObject && start && !start.disabled) {
              if (globalThis.__nextMediaScene) {
                if (!(scene instanceof HTMLSelectElement) || scene.disabled)
                  throw new Error('Replacement scene selector was unavailable');
                scene.value = globalThis.__nextMediaScene;
                globalThis.__nextMediaScene = null;
              }
              start.click();
            }
            if (video.srcObject && video.srcObject !== stale.stream && video.readyState >= 2) {
              const replacement = video.srcObject;
              stale.close.call(globalThis.__mediaSockets.at(-2), new CloseEvent('close'));
              stale.error.call(globalThis.__mediaSockets.at(-2), new Event('error'));
              statsState.release();
              if (!animation.held.cancelled)
                throw new Error('Old pointer animation frame was not cancelled');
              const socket = globalThis.__mediaSockets.at(-1);
              const sent = globalThis.__mediaSent.get(socket);
              const before = sent.filter(value => {
                try { return JSON.parse(value).type === 'pointer'; }
                catch { return false; }
              }).length;
              animation.held.callback(performance.now());
              await new Promise(resolve => setTimeout(resolve, 100));
              const after = sent.filter(value => {
                try { return JSON.parse(value).type === 'pointer'; }
                catch { return false; }
              }).length;
              if (after !== before)
                throw new Error('Old pointer callback sent to the replacement session');
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
                    statsMbps: mbps, statsFps: fps,
                    stalePointerCancelled: animation.held.cancelled,
                    replacementPointerMessages: after
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

    internal Task<JsonElement> SelectSceneAsync(
        BrowserScene scene,
        CancellationToken cancellation)
    {
        string value = scene == BrowserScene.Triangle ? "triangle" : "raymarching";
        return EvaluateAsync(
            $$"""
            (() => {
              const scene = document.getElementById('scene');
              const video = document.getElementById('video');
              if (!(scene instanceof HTMLSelectElement)
                  || !(video instanceof HTMLVideoElement)
                  || scene.disabled || video.srcObject)
                throw new Error('Scene can only change while stopped');
              scene.value = "{{value}}";
              globalThis.__nextMediaScene = "{{value}}";
              return scene.value;
            })()
            """,
            cancellation);
    }

    internal async Task<int> PointerMessageCountAsync(CancellationToken cancellation)
    {
        JsonElement count = await EvaluateAsync(
            """
            (() => {
              const socket = globalThis.__mediaSockets.at(-1);
              const sent = globalThis.__mediaSent.get(socket) ?? [];
              return sent.filter(value => {
                try { return JSON.parse(value).type === 'pointer'; }
                catch { return false; }
              }).length;
            })()
            """,
            cancellation);
        return count.GetInt32();
    }

    internal async Task WaitForPointerMessageAsync(
        int previousCount,
        CancellationToken cancellation)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (await PointerMessageCountAsync(cancellation) > previousCount)
            {
                return;
            }
            await Task.Delay(20, cancellation);
        }
        throw new InvalidOperationException("Browser pointer input was not sent.");
    }

    internal Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellation) =>
        browser.EvaluateAsync(expression, cancellation);

    internal async Task DragMouseAsync(
        double finalX,
        double finalY,
        CancellationToken cancellation)
    {
        await browser.CommandAsync("Page.bringToFront", new JsonObject(), cancellation);
        (double left, double top, double width, double height) =
            await GetVideoBoundsAsync(cancellation);
        double startX = left + width / 2;
        double startY = top + height / 2;
        await DispatchMouseAsync("mouseMoved", startX, startY, 0, cancellation);
        await DispatchMouseAsync("mousePressed", startX, startY, 1, cancellation);
        await DispatchMouseAsync(
            "mouseMoved",
            left + width * 0.65,
            top + height * 0.5,
            1,
            cancellation);
        await DispatchMouseAsync(
            "mouseReleased",
            left + width * finalX,
            top + height * finalY,
            0,
            cancellation);
    }

    internal async Task DragTouchAsync(
        double finalX,
        double finalY,
        CancellationToken cancellation)
    {
        await browser.CommandAsync("Page.bringToFront", new JsonObject(), cancellation);
        (double left, double top, double width, double height) =
            await GetVideoBoundsAsync(cancellation);
        JsonObject Point(double x, double y) => new()
        {
            ["x"] = x,
            ["y"] = y,
            ["id"] = 1,
            ["radiusX"] = 1,
            ["radiusY"] = 1,
            ["force"] = 1,
        };
        await browser.CommandAsync(
            "Input.dispatchTouchEvent",
            new JsonObject
            {
                ["type"] = "touchStart",
                ["touchPoints"] = new JsonArray(Point(left + width / 2, top + height / 2)),
            },
            cancellation);
        await browser.CommandAsync(
            "Input.dispatchTouchEvent",
            new JsonObject
            {
                ["type"] = "touchMove",
                ["touchPoints"] = new JsonArray(
                    Point(left + width * finalX, top + height * finalY)),
            },
            cancellation);
        await browser.CommandAsync(
            "Input.dispatchTouchEvent",
            new JsonObject
            {
                ["type"] = "touchEnd",
                ["touchPoints"] = new JsonArray(),
            },
            cancellation);
    }

    internal async Task<JsonElement> EvaluateForegroundAsync(
        string expression,
        CancellationToken cancellation)
    {
        await browser.CommandAsync("Page.bringToFront", new JsonObject(), cancellation);
        return await browser.EvaluateAsync(expression, cancellation);
    }

    private async Task<(double Left, double Top, double Width, double Height)> GetVideoBoundsAsync(
        CancellationToken cancellation)
    {
        JsonElement bounds = await EvaluateAsync(
            """
            (() => {
              const video = document.getElementById('video');
              if (!(video instanceof HTMLVideoElement)) throw new Error('Missing video');
              const bounds = video.getBoundingClientRect();
              return {left: bounds.left, top: bounds.top, width: bounds.width, height: bounds.height};
            })()
            """,
            cancellation);
        return (
            bounds.GetProperty("left").GetDouble(),
            bounds.GetProperty("top").GetDouble(),
            bounds.GetProperty("width").GetDouble(),
            bounds.GetProperty("height").GetDouble());
    }

    private Task<JsonElement> DispatchMouseAsync(
        string type,
        double x,
        double y,
        int buttons,
        CancellationToken cancellation) =>
        browser.CommandAsync(
            "Input.dispatchMouseEvent",
            new JsonObject
            {
                ["type"] = type,
                ["x"] = x,
                ["y"] = y,
                ["button"] = "left",
                ["buttons"] = buttons,
                ["clickCount"] = 1,
            },
            cancellation);

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

enum BrowserScene
{
    Triangle,
    Raymarching,
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
