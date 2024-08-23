using DualDrill.Graphics;

namespace DualDrill.Engine.Headless;

public readonly record struct HeadlessSurfaceFrame(
    GPUExtent3D Size,
    GPUTextureFormat Format,
    ReadOnlyMemory<byte> Data,
    int SlotIndex)
{
}
