using DualDrill.Engine.Connection;
using DualDrill.Engine.Input;
using MessagePipe;
using System.Reactive.Disposables;
using System.Threading.Channels;

namespace DualDrill.Engine.Services;

public sealed class FrameInputService : IDisposable
{
    CompositeDisposable Disposables = new();
    readonly Channel<PointerEvent> PointerEventChannel = Channel.CreateUnbounded<PointerEvent>();
    readonly Channel<ScaleEvent> ScaleEventChannel = Channel.CreateUnbounded<ScaleEvent>();
    readonly List<PointerEvent> PointerEventBuffer = new(128);
    public FrameInputService(
        ISubscriber<ClientEvent<PointerEvent>> PointerEventSubscriber,
        ISubscriber<ClientEvent<ScaleEvent>> ScaleEvents
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
    }

    public FrameInput ReadUserInputs()
    {
        PointerEventBuffer.Clear();
        var reader = PointerEventChannel.Reader;
        while (reader.TryRead(out var e))
        {
            PointerEventBuffer.Add(e);
        }
        float? scaleValue = null;
        while (ScaleEventChannel.Reader.TryRead(out var e))
        {
            scaleValue = e.Value;
        }
        return new(PointerEventBuffer.ToArray(), scaleValue);
    }

    public void Dispose()
    {
        Disposables.Dispose();
    }
}

public sealed record class FrameInput(
    ReadOnlyMemory<PointerEvent> PointerEvents,
    float? Scale
)
{
}
