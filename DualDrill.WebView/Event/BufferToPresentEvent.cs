using DualDrill.Engine;

namespace DualDrill.WebView.Event;

sealed record class BufferToPresentEvent(
    Guid SurfaceId,
    int Offset,
    int Length,
    int Tick,
    int Width,
    int Height
) : IHostToWebViewEvent
{
    public string MessageType { get; } = nameof(BufferToPresentEvent);
}
