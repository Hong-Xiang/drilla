using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;
public struct GPUVertexAttribute
{
    public GPUVertexFormat Format;

    [NativeTypeName("uint64_t")]
    public ulong Offset;

    [NativeTypeName("uint32_t")]
    public uint ShaderLocation;
}
