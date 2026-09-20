using System.Net.WebSockets;
using Gst;
using Gst.App;
using Gst.Sdp;
using Gst.WebRTC;

if (args is ["--self-test"])
{
    return CpuFrames.RunSelfTest();
}

GstSharpOptions nativeOptions = new();
GstApp.Initialize(nativeOptions);
GstSdp.Initialize(nativeOptions);
GstWebRTC.Initialize(nativeOptions);
WebRtcSession.EnsureNativeElements();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5084");

var app = builder.Build();
var gate = new ViewerGate();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(15),
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    object owner = new();

    if (!gate.TryAcquire(owner))
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsync("This proof of concept allows one viewer.", context.RequestAborted);
        return;
    }

    try
    {
        WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
        await using WebRtcSession session = new(
            socket,
            context.RequestServices.GetRequiredService<ILogger<WebRtcSession>>(),
            context.RequestAborted);
        await session.RunAsync();
    }
    finally
    {
        gate.Release(owner);
    }
});

await app.RunAsync();
return 0;

internal sealed class ViewerGate
{
    private object? _owner;

    internal bool TryAcquire(object owner) =>
        Interlocked.CompareExchange(ref _owner, owner, null) is null;

    internal void Release(object owner)
    {
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _owner, null, owner), owner))
        {
            throw new InvalidOperationException("The active viewer did not own the session gate.");
        }
    }
}
