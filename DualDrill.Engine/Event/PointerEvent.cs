namespace DualDrill.Engine.Event;

public enum PointerEventType : int
{
    Unknown,
    PointerDown,
    PointerMove,
    PointerUp,
    PointerDrag
}

public sealed record class PointerEvent(
    PointerEventType EventType,
    float X,
    float Y,
    float SurfaceWidth,
    float SurfaceHeight
)
{
}
