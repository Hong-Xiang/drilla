using DualDrill.Graphics;

namespace DualDrill.CLSL.Reflection;

public sealed record ShaderStorageBufferBinding(
    string Name,
    int Group,
    int Binding,
    GPUShaderStage Visibility,
    bool HasDynamicOffset,
    uint ElementStride,
    ulong MinimumBindingSize)
{
    public GPUBufferBindingType Kind => GPUBufferBindingType.ReadOnlyStorage;
}
