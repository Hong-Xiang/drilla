using DualDrill.Engine;
using DualDrill.Graphics.Headless;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace DualDrill.Server;

public sealed class HeadlessRealtimeFrameHostedService : BackgroundService
{
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    readonly TimeProvider TimeProvider = TimeProvider.System;

    private readonly ILogger<HeadlessRealtimeFrameHostedService> Logger;
    private readonly IFrameService FrameService;
    private readonly Channel<int> FrameChannel;

    private int FrameIndex = 0;

    private readonly HeadlessSurface Surface;

    public HeadlessRealtimeFrameHostedService(
        IFrameService frameService,
        ILogger<HeadlessRealtimeFrameHostedService> logger,
        HeadlessSurface surface)
    {
        Logger = logger;
        FrameService = frameService;
        FrameChannel = Channel.CreateBounded<int>(1);
        Surface = surface;
    }

    private static void TimerFrameCallback(object? data)
    {
        var self = (HeadlessRealtimeFrameHostedService)data!;
        if (!self.FrameChannel.Writer.TryWrite(self.FrameIndex))
        {
            self.Logger.LogWarning("Frame skipped {CurrentFrame}", self.FrameIndex);
        }
        self.FrameIndex++;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = TimeProvider.CreateTimer(TimerFrameCallback, this, TimeSpan.Zero, SampleRate);
        await foreach (var frameIndex in FrameChannel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            _ = Surface.TryAcquireImage(frameIndex);
            await FrameService.OnFrameAsync(new FrameContext
            {
                FrameIndex = frameIndex,
                MouseEvent = (MouseEvent[])[],
                Surface = Surface,
            }, stoppingToken).ConfigureAwait(false);
            await Surface.PresentAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
