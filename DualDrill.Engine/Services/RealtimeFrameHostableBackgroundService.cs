using DualDrill.Engine.Headless;
using DualDrill.Engine.Media;
using DualDrill.Engine.Scene;
using DualDrill.Graphics;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace DualDrill.Engine.Services;

public sealed class RealtimeFrameHostableBackgroundService(
    GPUDevice Device,
    FrameInputService FrameInputService,
    FrameSimulationService SimulationService,
    IFrameRenderService frameService,
    ILogger<RealtimeFrameHostableBackgroundService> logger,
    HeadlessSurface surface,
    IWebViewService WebViewService
    //HeadlessSurfaceCaptureVideoSource VideoSource
    ) : IHostableBackgroundService
{
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    readonly TimeProvider TimeProvider = TimeProvider.System;

    private readonly ILogger<RealtimeFrameHostableBackgroundService> Logger = logger;
    private readonly Channel<int> FrameChannel = Channel.CreateBounded<int>(1);

    private int FrameIndex = 0;

    private static void TimerFrameCallback(object? data)
    {
        var self = (RealtimeFrameHostableBackgroundService)data!;
        if (!self.FrameChannel.Writer.TryWrite(self.FrameIndex))
        {
            self.Logger.LogWarning("Frame skipped {CurrentFrame}", self.FrameIndex);
        }
        self.FrameIndex++;
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await WebViewService.StartAsync(stoppingToken);
        _ = WebViewService.CaptureAsync(surface, 30);
        //await VideoSource.StartVideo();
        using var timer = TimeProvider.CreateTimer(TimerFrameCallback, this, TimeSpan.Zero, SampleRate);

        var scene = RenderScene.TestScene(surface.Width, surface.Height);

        await foreach (var frameIndex in FrameChannel.Reader.ReadAllAsync(stoppingToken))
        {
            var inputs = FrameInputService.ReadUserInputs();
            scene = await SimulationService.SimulateAsync(frameIndex, inputs, scene);
            var image = surface.TryAcquireImage();
            if (image is null)
            {
                Logger.LogWarning("Failed to get surface texture for {frame}", frameIndex);
                continue;
            }
            await frameService.RenderAsync(frameIndex, scene, image.Texture, stoppingToken);
            surface.Present();
            Device.Poll();
        }
    }

}
