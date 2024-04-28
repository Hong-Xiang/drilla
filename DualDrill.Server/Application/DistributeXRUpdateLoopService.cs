using DualDrill.Engine.Connection;
using DualDrill.Server.Components.Pages;
using Microsoft.AspNetCore.Components;
using System.Threading.Channels;

namespace DualDrill.Server.Application;

sealed class FrameState
{
    public int Frame { get; set; } = 0;
}

public sealed class DistributeXRUpdateLoopService(ILogger<DistributeXRUpdateLoopService> Logger, ClientStore ClientStore) : BackgroundService
{
    readonly TimeProvider TimeProvider = TimeProvider.System;
    readonly Channel<int> FrameChannel = Channel.CreateBounded<int>(1);
    readonly Channel<int> RenderCommands = Channel.CreateUnbounded<int>();
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    public int FrameCount { get; private set; }
    public bool IsRendering { get; set; } = false;

    void FrameCallback(object? state)
    {
        if (state is FrameState frameState)
        {
            frameState.Frame++;
            FrameCount = frameState.Frame;
            if (!FrameChannel.Writer.TryWrite(frameState.Frame))
            {
                Logger.LogWarning("Skipped frame {FrameCount}", frameState.Frame);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var frameTimer = TimeProvider.CreateTimer(FrameCallback, new FrameState(), TimeSpan.Zero, SampleRate);
        while (!stoppingToken.IsCancellationRequested)
        {
            var frame = await FrameChannel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
            if (IsRendering)
            {
                RenderCommands.Writer.TryWrite(frame);
            }
        }
    }

    public IAsyncEnumerable<int> ReadAllRenderCommands() => RenderCommands.Reader.ReadAllAsync();
}


