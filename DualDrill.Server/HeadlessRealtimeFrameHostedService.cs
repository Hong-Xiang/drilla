
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
    private readonly Channel<uint> FrameChannel;

    private uint FrameIndex = 0;

    private readonly IOptions<HeadlessSurface.Option> CanvasOption;
    private readonly WGPUProviderService WGPUProviderService;
    private readonly HeadlessSurface Surface;

    public HeadlessRealtimeFrameHostedService(
        IFrameService frameService,
        ILogger<HeadlessRealtimeFrameHostedService> logger,
        IOptions<HeadlessSurface.Option> canvasOption,
        WGPUProviderService wGPUProviderService,
        HeadlessSurface surface)
    {
        Logger = logger;
        FrameService = frameService;
        FrameChannel = Channel.CreateBounded<uint>(1);
        CanvasOption = canvasOption;
        WGPUProviderService = wGPUProviderService;
        Surface = surface;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = TimeProvider.CreateTimer(static (object? state) =>
                {
                    var self = (HeadlessRealtimeFrameHostedService)state!;
                    if (!self.FrameChannel.Writer.TryWrite(self.FrameIndex))
                    {
                        self.Logger.LogWarning("Frame skipped {CurrentFrame}", self.FrameIndex);
                    }
                    self.FrameIndex++;
                }, this, TimeSpan.Zero, SampleRate);
        await Task.Yield();
        await foreach (var frameIndex in FrameChannel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            _ = Surface.TryAcquireImage();
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
