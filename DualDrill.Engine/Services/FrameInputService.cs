using DualDrill.Engine.Connection;
using DualDrill.Engine.Event;
using MessagePipe;
using System.Reactive.Disposables;
using System.Threading.Channels;

namespace DualDrill.Engine.Services;

public sealed class FrameInputService : IDisposable
{
    CompositeDisposable Disposables = new();
    readonly Channel<PointerEvent> PointerEventChannel = Channel.CreateUnbounded<PointerEvent>();
    readonly Channel<ScaleEvent> ScaleEventChannel = Channel.CreateUnbounded<ScaleEvent>();
    readonly Channel<CameraEvent> CameraEventChannel = Channel.CreateUnbounded<CameraEvent>();
    readonly List<PointerEvent> PointerEventBuffer = new(128);
    public FrameInputService(
        ISubscriber<ClientEvent<PointerEvent>> PointerEventSubscriber,
        ISubscriber<ClientEvent<ScaleEvent>> ScaleEvents,
        ISubscriber<ClientEvent<CameraEvent>> CameraEvents
    )
    {
        Disposables.Add(
            PointerEventSubscriber.Subscribe(x =>
            {
                PointerEventChannel.Writer.TryWrite(x.Payload);
            })
        );
        Disposables.Add(
            ScaleEvents.Subscribe(x =>
            {
                ScaleEventChannel.Writer.TryWrite(x.Payload);
            })
        );
        Disposables.Add(
            CameraEvents.Subscribe(x =>
            {
                CameraEventChannel.Writer.TryWrite(x.Payload);
            })
        );
    }

    public FrameInput ReadUserInputs()
    {
        PointerEventBuffer.Clear();
        var reader = PointerEventChannel.Reader;
        CameraEvent? cameraEvent = null;
        while (reader.TryRead(out var e))
        {
            PointerEventBuffer.Add(e);
        }
        float? scaleValue = null;
        while (ScaleEventChannel.Reader.TryRead(out var e))
        {
            scaleValue = e.Value;
        }
        while (CameraEventChannel.Reader.TryRead(out var e))
        {
            cameraEvent = e;
        }
        return new(PointerEventBuffer.ToArray(), scaleValue, cameraEvent);
    }

    public void Dispose()
    {
        Disposables.Dispose();
    }
}

public sealed record class FrameInput(
    ReadOnlyMemory<PointerEvent> PointerEvents,
    float? Scale,
    CameraEvent? CameraEvent
)
{
}
