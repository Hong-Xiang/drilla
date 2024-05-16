using DualDrill.Engine.Connection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Channels;

namespace DualDrill.Engine;
public readonly record struct MouseEvent(
    string Type,
    double ClientY,
    double ClientX,
    double ClientWidth,
    double ClientHeight
)
{
}


sealed class FrameState
{
    public int Frame { get; set; } = 0;
}

public record struct RenderState(float Time, float[] State)
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
            //var reader = MouseEvent;
            var reader = mouseEventReader;
            if (reader is not null)
            {
                while (reader.TryRead(out var e))
                {
                    eventCount++;
                    //MouseEvents.Writer.TryWrite(e);
                    //Logger.LogInformation("MouseEvent {Event}", e);
                }
                //reader = MouseEvents.Reader;
                //while (reader.TryRead(out var e))
                //{
                //    if (e.Type == "mousedown" || e.Type == "mouseup")
                //    {
                //        Console.WriteLine(e.Type);
                //    }
                //    eventCount++;
                //}
            }

            var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI * 2.0f / 5.0f,
                8.0f / 6.0f,
                1.0f,
                100.0f
            );
            var viewMatrix = Matrix4x4.CreateLookAt(
                  new Vector3(0, 0, -4),
                  Vector3.Zero,
                  Vector3.UnitY);
            var rotateValue = frame / 60.0f;
            var rotate = Matrix4x4.CreateFromYawPitchRoll(
                MathF.Sin(rotateValue),
                MathF.Cos(rotateValue),
                0
            );
            var mvpMatrix = rotate * viewMatrix * projMatrix;
            //var mvpMatrix = projMatrix * viewMatrix * rotate;

            var buffer = CopyToBuffer(mvpMatrix);

            if (eventCount > 0)
            {
                Logger.LogInformation("MouseEvent Count {count}", eventCount);
            }

            await RenderStates.Writer.WriteAsync(new RenderState(frame, buffer), stoppingToken).ConfigureAwait(false);
        }
    }

    private unsafe float[] CopyToBuffer(Matrix4x4 m)
    {
        var result = new float[16];
        var sourceBuffer = new Span<float>(&m, 16);
        sourceBuffer.CopyTo(result);
        return result;
    }
}


