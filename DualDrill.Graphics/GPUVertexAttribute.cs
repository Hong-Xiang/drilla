using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;
public struct GPUVertexAttribute
{
    public GPUVertexFormat Format { get; set; }

    [NativeTypeName("uint64_t")]
    public ulong Offset { get; set; }

    [NativeTypeName("uint32_t")]
    public uint ShaderLocation { get; set; }
}
