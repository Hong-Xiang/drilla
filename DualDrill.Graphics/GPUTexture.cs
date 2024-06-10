using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUTexture
{
    public unsafe GPUTextureView CreateView(GPUTextureViewDescriptor? descriptor)
    {
        if (descriptor.HasValue)
        {
            throw new NotImplementedException();
        }
        return new(WGPU.wgpuTextureCreateView(Handle, null));
    }
}
