using DualDrill.Graphics;

namespace DualDrill.CLSL.Reflection;

public sealed record ShaderStorageBufferBinding(
    string Name,
    int Group,
    int Binding,
    GPUShaderStage Visibility,
    bool HasDynamicOffset,
    GPUBufferBindingType Kind,
    uint ElementStride,
    ulong MinimumBindingSize);
