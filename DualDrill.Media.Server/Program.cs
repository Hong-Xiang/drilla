using System.Net.WebSockets;
using Gst;
using Gst.App;
using Gst.Sdp;
using Gst.WebRTC;

if (args is ["--self-test"])
{
    return CpuFrames.RunSelfTest() == 0 &&
        VideoSettings.RunSelfTest() == 0 &&
        SessionSettings.RunSelfTest() == 0
        ? WebRtcSession.RunSignalSelfTest()
        : 1;
}

if (args is ["--gpu-self-test"])
{
    return await GpuFrames.RunSelfTestAsync();
}

if (args is ["--native-self-test"])
{
    InitializeGStreamer();
    return NativeFrameCheck.Run();
}

var builder = WebApplication.CreateBuilder(args);
VideoSettings video = VideoSettings.Load(builder.Configuration);
SessionSettings sessions = SessionSettings.Load(builder.Configuration);
if (builder.Configuration["urls"] is null)
{
    builder.WebHost.UseUrls("http://127.0.0.1:5084");
}

InitializeGStreamer();
WebRtcSession.EnsureNativeElements();

var app = builder.Build();
var gate = new SessionGate(sessions.MaxConcurrent);

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

    using SessionAdmission? admission = gate.TryAcquire();
    if (admission is null)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsync(
            $"All {sessions.MaxConcurrent} media session slots are in use.",
            context.RequestAborted);
        return;
    }

    WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    await using WebRtcSession session = new(
        socket,
        context.RequestServices.GetRequiredService<ILogger<WebRtcSession>>(),
        video,
        context.RequestAborted);
    await session.RunAsync();
});

await app.RunAsync();
return 0;

static void InitializeGStreamer()
{
    GstSharpOptions nativeOptions = new();
    GstApp.Initialize(nativeOptions);
    GstSdp.Initialize(nativeOptions);
    GstWebRTC.Initialize(nativeOptions);
}
