namespace DualDrill.Engine.Input;

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
