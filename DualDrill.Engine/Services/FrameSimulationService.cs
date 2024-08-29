using DualDrill.Engine.Event;
using DualDrill.Engine.Scene;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Engine.Services;

public sealed class FrameSimulationService(
    FrameInputService InputService,
    ILogger<FrameSimulationService> Logger)
{
    public Channel<RenderScene> RenderStates { get; }
        = Channel.CreateBounded<RenderScene>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    float m_Scale = 1.0f;
    public float Scale
    {
        get => m_Scale;
        set
        {
            m_Scale = value;
        }
    }

    public async IAsyncEnumerable<float> CubeScaleChanges(float initialValue, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var current = initialValue;
        var channel = Channel.CreateBounded<float>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        if (!ScaleChangeSubscriptions.TryAdd(cancellation, channel))
        {
            yield break;
        }
        else
        {
            var value = await channel.Reader.ReadAsync(cancellation).ConfigureAwait(false);
            if (value != current)
            {
                yield return value;
                current = value;
            }
        }
    }

    ConcurrentDictionary<CancellationToken, Channel<float>> ScaleChangeSubscriptions = [];

    public async ValueTask<RenderScene> SimulateAsync(long frame, FrameInput frameInput, RenderScene scene)
    {
        var t = (float)frame / 60.0f;
        var events = frameInput.PointerEvents;
        var eventCount = events.Length;
        var r = new Vector3(MathF.Sin(t), MathF.Cos(t), 0);
        if (frameInput.CameraEvent is CameraEvent e)
        {
            scene = scene with
            {
                Camera = scene.Camera with
                {
                    Position = e.Position,
                    Forward = e.Forward,
                    Up = e.Up
                }
            };
        }
        var p = new Vector3(10.0f * MathF.Cos(t), 5.0f, 10.0f * MathF.Sin(t));
        scene = scene with
        {

            Camera = scene.Camera with
            {
                Position = p,
                Forward = Vector3.Zero - p
            }
        };
        scene = scene with { Cube = scene.Cube with { Rotation = r, Scale = frameInput.Scale ?? scene.Cube.Scale } };
        scene = scene with { ClearColor = new Vector3(0.2f) + 0.1f * new Vector3(MathF.Cos(t), MathF.Sin(t), 0.1f) };
        if (eventCount > 0)
        {
            var lastE = events.Span[^1];
            Vector3 pos = new(lastE.X, lastE.Y, 0.0f);
            scene = scene with
            {
                Cube = scene.Cube with { Position = pos },
                LogoState = scene.LogoState with
                {
                    Position = new Vector2(pos.X, pos.Y)
                }
            };
        }
        else
        {
            scene = scene with
            {
                LogoState = scene.LogoState with
                {
                    Position = scene.LogoState.Position + 0.005f * new Vector2(MathF.Cos((float)t), MathF.Sin((float)t)),
                }
            };
        }
        return scene;
    }
}


