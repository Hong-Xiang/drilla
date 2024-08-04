using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DualDrill.Server.CustomEvents;

[EventHandler("onnormalizedpointermove", typeof(NormalizedPointerEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onnormalizedpointerdown", typeof(NormalizedPointerEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onnormalizedpointerup", typeof(NormalizedPointerEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}

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


public sealed class NormalizedPointerEventArgs : PointerEventArgs
{
    public required DOMRect BoundingRect { get; init; }
    public override string? ToString()
    {
        return $"{nameof(NormalizedPointerEventArgs)}(OffsetX = {OffsetX}, OffsetY = {OffsetY}, BoundingRect = {BoundingRect})";
    }
}
