using DualDrill.Graphics;

namespace DualDrill.CLSL.Reflection;

public sealed record ShaderTextureBinding(
    string Name,
    int Group,
    int Binding,
    GPUShaderStage Visibility)
{
    public BindGroupLayoutEntryFlag Kind => BindGroupLayoutEntryFlag.Texture;
    public GPUTextureViewDimension Dimension => GPUTextureViewDimension._2D;
    public GPUTextureSampleType SampleType => GPUTextureSampleType.Float;
    public bool Multisampled => false;
}

public sealed record ShaderSamplerBinding(
    string Name,
    int Group,
    int Binding,
    GPUShaderStage Visibility)
{
    public BindGroupLayoutEntryFlag Kind => BindGroupLayoutEntryFlag.Sampler;
    public GPUSamplerBindingType SamplerType => GPUSamplerBindingType.Filtering;
    public bool Comparison => false;
}
