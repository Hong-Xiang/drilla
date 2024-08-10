using DualDrill.Engine.Input;
using DualDrill.Graphics;
using System.Numerics;

namespace DualDrill.Engine;

public readonly record struct FrameContext(
    long FrameIndex,
    ReadOnlyMemory<PointerEvent> PointerEvent,
    IGPUSurface Surface,
    Vector3 Position)
{
}
