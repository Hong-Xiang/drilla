namespace DualDrill.Graphics;

public enum BindGroupLayoutEntryFlag
{
    Buffer = 0,
    Sampler = 1,
    Texture = 2,
    StorageTexture = 3,
    All = 4
}

public partial struct GPUBindGroupLayoutEntry
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUBufferBindingLayout Buffer { get; set; }
    public GPUSamplerBindingLayout Sampler { get; set; }
    public GPUTextureBindingLayout Texture { get; set; }
    public GPUStorageTextureBindingLayout StorageTexture { get; set; }
}

public partial struct GPUBindGroupLayoutEntryBuffer
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUBufferBindingLayout Buffer { get; set; }
}

public partial struct GPUBindGroupLayoutEntrySamper
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUSamplerBindingLayout Sampler { get; set; }
}

public partial struct GPUBindGroupLayoutEntryTexture
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUTextureBindingLayout Texture { get; set; }
}

public partial struct GPUBindGroupLayoutEntryStorageTexure
{
    public GPUShaderStage Visibility { get; set; }
    public int Binding { get; set; }
    public GPUStorageTextureBindingLayout StorageTexture { get; set; }
}