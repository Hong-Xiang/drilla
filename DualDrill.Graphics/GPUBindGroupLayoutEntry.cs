namespace DualDrill.Graphics;

public partial struct GPUBindGroupLayoutEntry
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUBufferBindingLayout Buffer { get; set; }
    public GPUSamplerBindingLayout Sampler { get; set; }
    public GPUTextureBindingLayout Texture { get; set; }
    public GPUStorageTextureBindingLayout StorageTexture { get; set; }
}