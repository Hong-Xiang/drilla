using DualDrill.Engine;

namespace DualDrill.WebView.Event;

public sealed record class SignalConnectionEvent<T>(
    Guid ClientId,
    T Payload
) : IHostToWebViewEvent
{
    public string MessageType { get; } = nameof(T);
}
