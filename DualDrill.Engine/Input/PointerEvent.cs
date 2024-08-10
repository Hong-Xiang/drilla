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
    int X,
    int Y,
    int Width,
    int Height
)
{
}
