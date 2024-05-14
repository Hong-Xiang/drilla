using Microsoft.AspNetCore.Components;

namespace DualDrill.Server.CustomEvents;

[EventHandler("onnormalizedpointermove", typeof(NormalizedPointEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onnormalizedpointerdown", typeof(NormalizedPointEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onnormalizedpointerup", typeof(NormalizedPointEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}

public sealed class NormalizedPointEventArgs : EventArgs
{
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public int OffsetWidth { get; set; }
    public int OffsetHeight { get; set; }

    public override string? ToString()
    {
        return $"{nameof(NormalizedPointEventArgs)}(X: {OffsetX}, Y: {OffsetY}, Width: {OffsetWidth}, Height {OffsetHeight})";
    }
}
