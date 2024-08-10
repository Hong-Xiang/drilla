using DualDrill.Engine;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Numerics;
using System.Threading.Channels;

namespace DualDrill.Server.Services;

public sealed class RealtimeFrameHostedService : BackgroundService
{
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    readonly TimeProvider TimeProvider = TimeProvider.System;

    private readonly ILogger<RealtimeFrameHostedService> Logger;
    private readonly IFrameService FrameService;
    private readonly Channel<int> FrameChannel;

    private int FrameIndex = 0;

    private readonly HeadlessSurface Surface;

    public FrameInputService FrameInputService { get; }
    public IWebViewService WebViewService { get; }
    public HeadlessSurfaceCaptureVideoSource VideoSource { get; }
    private IHostApplicationLifetime HostApplicationLifetime { get; }
    private IServer WebServer { get; }

    public RealtimeFrameHostedService(
        IFrameService frameService,
        FrameInputService frameInputService,
        ILogger<RealtimeFrameHostedService> logger,
        HeadlessSurface surface,
        IWebViewService webViewService,
        HeadlessSurfaceCaptureVideoSource videoSource,
        IHostApplicationLifetime hostApplicationLifetime,
        IServer webServer)
    {
        Logger = logger;
        FrameService = frameService;
        FrameInputService = frameInputService;
        FrameChannel = Channel.CreateBounded<int>(1);
        Surface = surface;
        VideoSource = videoSource;
        WebViewService = webViewService;
        HostApplicationLifetime = hostApplicationLifetime;
        WebServer = webServer;
    }

    private static void TimerFrameCallback(object? data)
    {
        var self = (RealtimeFrameHostedService)data!;
        if (!self.FrameChannel.Writer.TryWrite(self.FrameIndex))
        {
            self.Logger.LogWarning("Frame skipped {CurrentFrame}", self.FrameIndex);
        }
        self.FrameIndex++;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var hostUri = await GetHostedSourceUriAsync(stoppingToken);
        //await WebViewService.StartAsync(hostUri, stoppingToken);
        await VideoSource.StartVideo();
        using var timer = TimeProvider.CreateTimer(TimerFrameCallback, this, TimeSpan.Zero, SampleRate);


        var frameContext = new FrameContext
        {
            Position = Vector3.Zero,
            PointerEvent = FrameInputService.ReadUserInputs(),
            Surface = Surface,
        };

        await foreach (var frameIndex in FrameChannel.Reader.ReadAllAsync(stoppingToken))
        {
            frameContext = frameContext with { FrameIndex = frameIndex, PointerEvent = FrameInputService.ReadUserInputs() };
            var surfaceImage = Surface.TryAcquireImage();
            if (surfaceImage is null)
            {
                Logger.LogWarning("Failed to get render target from headless surface");
            }
            frameContext = await FrameService.OnFrameAsync(frameContext, stoppingToken);
            Surface.Present();
        }
    }

    async ValueTask<Uri> GetHostedSourceUriAsync(CancellationToken cancellation)
    {
        var tcs = new TaskCompletionSource(cancellation);
        HostApplicationLifetime.ApplicationStarted.Register(tcs.SetResult);
        await tcs.Task;
        var address = WebServer.Features.Get<IServerAddressesFeature>();
        var uri = (address?.Addresses.FirstOrDefault(x => new Uri(x).Scheme == "https")) ?? throw new ArgumentNullException("WebView2 Source Uri");
        return new Uri(uri + "/webview2");
    }
}
