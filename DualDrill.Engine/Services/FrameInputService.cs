using DualDrill.Engine.Input;
using MessagePipe;
using System.Reactive.Disposables;
using System.Threading.Channels;

namespace DualDrill.Engine.Services;

public sealed class FrameInputService : IDisposable
{
    CompositeDisposable Disposables = new();
    readonly Channel<PointerEvent> PointerEventChannel = Channel.CreateUnbounded<PointerEvent>();
    readonly List<PointerEvent> PointerEventBuffer = new(128);
    public FrameInputService(
        ISubscriber<ClientInput<PointerEvent>> PointerEventSubscriber
    )
    {
        Disposables.Add(
            PointerEventSubscriber.Subscribe(x =>
            {
                PointerEventChannel.Writer.TryWrite(x.Payload);
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
        return new(PointerEventBuffer.ToArray());
    }

    public void Dispose()
    {
        Disposables.Dispose();
    }
}

public sealed record class FrameInput(
    ReadOnlyMemory<PointerEvent> PointerEvents
)
{
}
