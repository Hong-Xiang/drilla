using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

//public partial struct GPUBindGroupLayoutEntry()
//{
//    public int Binding { get; set; }
//    public GPUBufferBindingLayout Buffer { get; set; }
//    //public GPUExternalTextureBindingLayout ExternalTexture { get; set; }
//    public GPUSamplerBindingLayout Sampler { get; set; }
//    public GPUStorageTextureBindingLayout StorageTexture { get; set; }
//    public GPUTextureBindingLayout Texture { get; set; }
//    public GPUShaderStage Visibility { get; set; }
//}

//public partial struct GPUBindGroupEntry()
//{
//    public int Binding { get; set; }
//    public GPUBindingResource Resource { get; set; }
//}


public partial struct GPUTextureDescriptor()
{
    public required GPUTextureUsage Usage { get; set; }
    public int MipLevelCount { get; set; } = 1;
    public int SampleCount { get; set; } = 1;
    public string Label { get; set; } = string.Empty;
    public GPUTextureDimension Dimension { get; set; } = GPUTextureDimension._2D;
    public required GPUExtent3D Size { get; set; }
    public required GPUTextureFormat Format { get; set; }
    public ReadOnlyMemory<GPUTextureFormat> ViewFormats { get; set; }
}

