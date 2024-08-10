using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DualDrill.Engine;

public sealed class FrameSimulationService(
    FrameInputService InputService,
    ILogger<FrameSimulationService> Logger)
{
    public int FrameCount { get; private set; }


    public Channel<RenderScene> RenderStates { get; }
        = Channel.CreateBounded<RenderScene>(new BoundedChannelOptions(3)
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
            StateChangeEvent?.Invoke(value);
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

    public float[] CubeSimulation(FrameContext context, out FrameContext updated)
    {
        var events = context.PointerEvent;
        var eventCount = events.Length;
        if (eventCount > 0)
        {

            Logger.LogInformation("Event Count {Count}", eventCount);
        }
        //var p = Matrix4x4.CreatePerspective(
        //              6.4f * 0.1f,
        //              4.8f * 0.1f,
        //              0.1f,
        //              20
        //          );
        var p = Matrix4x4.CreateScale(
               1.0f / 6.4f,
               1.0f / 4.8f,
               0.0f
        );
        var c = Matrix4x4.CreateLookAt(new Vector3
        {
            X = 1,
            Y = 0.8f,
            Z = 1
        }, Vector3.Zero, Vector3.UnitY);
        var t = context.FrameIndex / 60.0f;
        var trans = Matrix4x4.Identity;
        updated = context;
        if (eventCount > 0)
        {
            var lastE = events.Span[^1];
            Vector3 pos = new(lastE.X / lastE.SurfaceWidth + 0.5f,
               lastE.Y / lastE.SurfaceHeight + 0.5f,
               0.0f);
            trans = Matrix4x4.CreateTranslation(pos);
            updated = updated with { Position = pos };
            Logger.LogInformation("pos {}", pos);
        }
        var m = Matrix4x4.CreateFromYawPitchRoll(MathF.Sin(t), MathF.Cos(t), 0);
        var s = Matrix4x4.CreateScale(Scale);
        var mvp = s * m * trans * c * p;

        //var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
        //    MathF.PI * 2.0f / 5.0f,
        //    8.0f / 6.0f,
        //    1.0f,
        //    100.0f
        //);
        //var viewMatrix = Matrix4x4.CreateLookAt(
        //      new Vector3(0, 0, -4),
        //      Vector3.Zero,
        //      Vector3.UnitY);
        //var rotateValue = context.FrameIndex / 60.0f;
        //var rotate = Matrix4x4.CreateFromYawPitchRoll(
        //    MathF.Sin(rotateValue),
        //    MathF.Cos(rotateValue),
        //    0
        //);
        //var mvpMatrix = rotate * viewMatrix * projMatrix;
        ////var mvpMatrix = projMatrix * viewMatrix * rotate;

        var buffer = CopyToBuffer(mvp);
        return buffer;
        //var scene = new RenderScene()
        //{
        //    Frame = context.FrameIndex,
        //    Camera = new Camera()
        //    {
        //        NearPlaneWidth = 8,
        //        NearPlaneHeight = 6,
        //        NearPlaneDistance = 1,
        //        FarPlaneDistance = 100,
        //        Position = new Vector3(0, 0, -4),
        //        Target = Vector3.Zero,
        //        Up = Vector3.UnitY
        //    },
        //    Cube = new Cube()
        //    {
        //        Scale = 1.0f,
        //        Rotation = new()
        //        {
        //            X = MathF.Sin(rotateValue),
        //            Y = MathF.Cos(rotateValue),
        //            Z = 0
        //        }
        //    }
        //};
        //foreach (var (_, c) in ScaleChangeSubscriptions)
        //{
        //    //c.Writer.TryWrite()
        //}
    }

    private unsafe float[] CopyToBuffer(Matrix4x4 m)
    {
        var result = new float[16];
        var sourceBuffer = new Span<float>(&m, 16);
        sourceBuffer.CopyTo(result);
        return result;
    }
}


