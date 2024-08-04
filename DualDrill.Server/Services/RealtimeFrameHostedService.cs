using DualDrill.Engine;
using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
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

    public HeadlessSurfaceCaptureVideoSource VideoSource { get; }

    public RealtimeFrameHostedService(
        IFrameService frameService,
        ILogger<RealtimeFrameHostedService> logger,
        HeadlessSurface surface,
        HeadlessSurfaceCaptureVideoSource videoSource)
    {
        Logger = logger;
        FrameService = frameService;
        FrameChannel = Channel.CreateBounded<int>(1);
        Surface = surface;
        VideoSource = videoSource;
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
        await VideoSource.StartVideo();
        using var timer = TimeProvider.CreateTimer(TimerFrameCallback, this, TimeSpan.Zero, SampleRate);
        await foreach (var frameIndex in FrameChannel.Reader.ReadAllAsync(stoppingToken))
        {
            var surfaceImage = Surface.TryAcquireImage();
            if (surfaceImage is null)
            {
                Logger.LogWarning("Failed to get render target from headless surface");
            }
            await FrameService.OnFrameAsync(new FrameContext
            {
                FrameIndex = frameIndex,
                MouseEvent = (MouseEvent[])[],
                Surface = Surface,
            }, stoppingToken);
            Surface.Present();
        }
    }
}
