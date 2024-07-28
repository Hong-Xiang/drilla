using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public unsafe sealed class ShaderModule(
    Silk.NET.WebGPU.WebGPU Api,
    Silk.NET.WebGPU.ShaderModule* Handle
)
{
    public Silk.NET.WebGPU.ShaderModule* Handle { get; } = Handle;

}

public sealed partial class GPUShaderModule
{
    internal unsafe static GPUShaderModule Create(GPUDevice device, string code)
    {
        using var nativeCode = NativeStringRef.Create(code);
        var wgslDescriptor = new WGPUShaderModuleWGSLDescriptor
        {
            code = (sbyte*)nativeCode.Handle,
            chain = new WGPUChainedStruct
            {
                sType = GPUSType.ShaderModuleWGSLDescriptor
            }
        };

        var shaderModuleDescriptor = new WGPUShaderModuleDescriptor
        {
            nextInChain = &wgslDescriptor.chain,
        };

        var handle = WGPU.DeviceCreateShaderModule(device.NativePointer, &shaderModuleDescriptor);
        return new GPUShaderModule(handle);
    }
}
