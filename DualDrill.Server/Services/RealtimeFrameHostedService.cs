using DualDrill.Engine;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using DualDrill.Engine.Scene;
using DualDrill.Engine.Services;
using System.Threading.Channels;

namespace DualDrill.Server.Services;

public sealed class RealtimeFrameHostedService : BackgroundService
{
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    readonly TimeProvider TimeProvider = TimeProvider.System;

    private readonly ILogger<RealtimeFrameHostedService> Logger;
    private readonly IFrameRenderService FrameService;
    private readonly Channel<int> FrameChannel;

    private int FrameIndex = 0;

    private readonly HeadlessSurface Surface;

    public FrameInputService FrameInputService { get; }
    public FrameSimulationService SimulationService { get; }
    public IWebViewService WebViewService { get; }
    public HeadlessSurfaceCaptureVideoSource VideoSource { get; }

    public RealtimeFrameHostedService(
        FrameInputService frameInputService,
        FrameSimulationService simulationService,
        IFrameRenderService frameService,
        ILogger<RealtimeFrameHostedService> logger,
        HeadlessSurface surface,
        IWebViewService webViewService,
        HeadlessSurfaceCaptureVideoSource videoSource)
    {
        Logger = logger;
        FrameService = frameService;
        FrameInputService = frameInputService;
        SimulationService = simulationService;
        FrameChannel = Channel.CreateBounded<int>(1);
        Surface = surface;
        VideoSource = videoSource;
        WebViewService = webViewService;
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
        //await WebViewService.StartAsync(stoppingToken);
        await VideoSource.StartVideo();
        using var timer = TimeProvider.CreateTimer(TimerFrameCallback, this, TimeSpan.Zero, SampleRate);


        var scene = RenderScene.TestScene(Surface.Width, Surface.Height);

        await foreach (var frameIndex in FrameChannel.Reader.ReadAllAsync(stoppingToken))
        {
            var inputs = FrameInputService.ReadUserInputs();
            scene = await SimulationService.SimulateAsync(frameIndex, inputs, scene);
            var image = Surface.TryAcquireImage();
            if (image is null)
            {
                Logger.LogWarning("Failed to get surface texture for {frame}", frameIndex);
                continue;
            }
            await FrameService.RenderAsync(frameIndex, scene, image.Texture, stoppingToken);
            Surface.Present();
        }
    }

}
