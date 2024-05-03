using DualDrill.Engine.Connection;
using DualDrill.Server.Browser;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;

namespace DualDrill.Server.Application;

sealed class FrameState
{
    public int Frame { get; set; } = 0;
}

public sealed class DistributeXRApplication(ILogger<DistributeXRApplication> Logger, ClientStore ClientStore) : BackgroundService
{
    readonly TimeProvider TimeProvider = TimeProvider.System;
    readonly Channel<int> FrameChannel = Channel.CreateBounded<int>(1);
    readonly Channel<int> RenderCommands = Channel.CreateUnbounded<int>();
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    public int FrameCount { get; private set; }
    public JSRenderService? RenderService { get; set; } = default;

    public event PropertyChangedEventHandler PropertyChanged;



    event Action<float> StateChangeEvent;
    float m_Scale = 1.0f;
    public float Scale
    {
        get => m_Scale;
        set
        {
            m_Scale = value;
            StateChangeEvent(value);
        }
    }

    public async IAsyncEnumerable<T> Where<T>(IAsyncEnumerable<T> source, Func<T, bool> predicator)
    {
        await foreach (var item in source)
        {
            if (predicator(item))
            {
                yield return item;
            }
        }
    }

    public void Test(IAsyncEnumerable<int> data)
    {
        var evenData = Where(data, x => x % 2 == 0);
    }


    public async IAsyncEnumerable<float> ScaleChanges(CancellationToken token)
    {
        var channel = Channel.CreateBounded<float>(1);
        var h = (float value) =>
        {
            channel.Writer.TryWrite(value);
        };
        StateChangeEvent += h;
        yield return m_Scale;

        while (!token.IsCancellationRequested)
        {
            yield return await channel.Reader.ReadAsync();
        }
        StateChangeEvent -= h;
    }


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
            var rs = RenderService;

            if (rs is not null)
            {
                await rs.Render(frame, Scale);
            }
            RenderCommands.Writer.TryWrite(frame);
        }
    }
}


