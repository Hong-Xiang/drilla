#:property TreatWarningsAsErrors=true
#:property TargetFramework=net10.0

using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run -p:ImportDirectoryPackagesProps=false script/gstsharp-browser-smoke.cs -- <server-url> <chromium-debug-url>");
    return 2;
}

var server = new Uri(args[0]);
var debugger = new Uri(args[1]);
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
                const NativeWebSocket = globalThis.WebSocket;
                globalThis.WebSocket = class extends NativeWebSocket {
                  constructor(url) { super(url); globalThis.__mediaSockets.push(this); }
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
              const canvas = document.createElement('canvas');
              const context = canvas.getContext('2d', {willReadFrequently: true});
              if (!context) throw new Error('Canvas unavailable');
              function sample() {
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                context.drawImage(video, 0, 0);
                const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
                let red = 0, green = 0, blue = 0, yellow = 0, white = 0, whiteX = 0, whiteY = 0;
                for (let i = 0; i < pixels.length; i += 4) {
                  const r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
                  const x = (i / 4) % canvas.width, y = Math.floor(i / 4 / canvas.width);
                  const left = x < canvas.width / 2, top = y < canvas.height / 2;
                  if (left && top && r > 150 && g < 100 && b < 100) red++;
                  if (!left && top && g > 150 && r < 100 && b < 100) green++;
                  if (left && !top && b > 150 && r < 100 && g < 100) blue++;
                  if (!left && !top && r > 150 && g > 150 && b < 100) yellow++;
                  if (r > 210 && g > 210 && b > 210) { white++; whiteX += x; whiteY += y; }
                }
                return {red, green, blue, yellow, white, markerX: whiteX / white, markerY: whiteY / white};
              }
              for (let i = 0; i < 200; i++) {
                if (video.readyState >= 2 && video.getVideoPlaybackQuality().totalVideoFrames >= 10) {
                  const first = sample();
                  await new Promise(resolve => setTimeout(resolve, 500));
                  const second = sample();
                  if (first.white < 300 || second.white < 300)
                    throw new Error('CPU-generated white marker was not decoded');
                  if (Math.hypot(first.markerX - second.markerX, first.markerY - second.markerY) < 4)
                    throw new Error('Received marker did not move');
                  if (Math.min(second.red, second.green, second.blue, second.yellow) < 10000)
                    throw new Error('Expected CPU-generated RGB regions were not decoded');
                  return {
                    width: video.videoWidth, height: video.videoHeight,
                    decodedFrames: video.getVideoPlaybackQuality().totalVideoFrames,
                    ...second
                  };
                }
                await new Promise(resolve => setTimeout(resolve, 100));
              }
              throw new Error('No decoded video: ' + document.getElementById('status')?.textContent);
            })()
            """, cancellation);
        if (observation.GetProperty("width").GetInt32() != 320
            || observation.GetProperty("height").GetInt32() != 240)
        {
            throw new InvalidOperationException($"Unexpected frame dimensions: {observation}");
        }
        previousFrame = observation.GetProperty("decodedFrames").GetInt64();
        Console.WriteLine($"Connection {iteration + 1}: {observation}");

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
                  document.getElementById('stop').click();
                  await new Promise(resolve => setTimeout(resolve, 500));
                  document.getElementById('start').click();
                  oldClose.call(oldSocket, new CloseEvent('close'));
                  oldError.call(oldSocket, new Event('error'));
                  const video = document.getElementById('video');
                  for (let i = 0; i < 200; i++) {
                    if (video.srcObject && video.readyState >= 2
                        && video.getVideoPlaybackQuality().totalVideoFrames >= 3) return true;
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

Console.WriteLine($"PASS: CPU colors and moving marker decoded across four connections, including tab-close recovery; last count {previousFrame}.");
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
