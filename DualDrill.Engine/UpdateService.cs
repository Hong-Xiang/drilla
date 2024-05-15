using DualDrill.Engine.Connection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Threading.Channels;

namespace DualDrill.Engine;
public readonly record struct MouseEvent(
    string Type,
    double ClientY,
    double ClientX
)
{
}


sealed class FrameState
{
    public int Frame { get; set; } = 0;
}

public record struct RenderState(float Time, float State)
{
}

public sealed class UpdateService(ILogger<UpdateService> Logger, ClientStore ClientStore) : BackgroundService
{
    readonly TimeProvider TimeProvider = TimeProvider.System;
    readonly Channel<int> FrameChannel = Channel.CreateBounded<int>(1);
    readonly TimeSpan SampleRate = TimeSpan.FromSeconds(1.0 / 60.0);
    public int FrameCount { get; private set; }

    public event PropertyChangedEventHandler PropertyChanged;


    public readonly Channel<MouseEvent> MouseEvents = Channel.CreateUnbounded<MouseEvent>(new UnboundedChannelOptions
    {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = false
    });

    public ChannelReader<MouseEvent>? MouseEvent { get; set; }

    public Channel<RenderState> RenderStates { get; }
        = Channel.CreateBounded<RenderState>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

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
        var mouseEventReader = MouseEvents.Reader;
        while (!stoppingToken.IsCancellationRequested)
        {
            var frame = await FrameChannel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);

            var eventCount = 0;
            var reader = MouseEvent;
            if (reader is not null)
            {
                //while (reader.TryRead(out var e))
                //{
                //    MouseEvents.Writer.TryWrite(e);
                //    //Logger.LogInformation("MouseEvent {Event}", e);
                //}
                //reader = MouseEvents.Reader;
                while (reader.TryRead(out var e))
                {
                    if (e.Type == "mousedown" || e.Type == "mouseup")
                    {
                        Console.WriteLine(e.Type);
                    }
                    eventCount++;
                }
            }

            if (eventCount > 0)
            {
                Logger.LogInformation("MouseEvent Count {count}", eventCount);
            }

            await RenderStates.Writer.WriteAsync(new RenderState(frame, 1.0f), stoppingToken).ConfigureAwait(false);
        }
    }
}


