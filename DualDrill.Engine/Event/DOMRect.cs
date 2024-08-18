namespace DualDrill.Engine.Message;

public sealed record class DOMRect(
    float X,
    float Y,
    float Width,
    float Height,
    float Top,
    float Right,
    float Bottom,
    float Left
)
{
}
