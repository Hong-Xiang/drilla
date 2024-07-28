using DualDrill.Graphics.Interop;
using DualDrill.Interop;
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
        var codeUtf8 = Utf8String.Create(code);
        using var nativeCode = codeUtf8.Memory.Pin();
        var descriptor = new WGPUShaderModuleWGSLDescriptor
        {
            code = (sbyte*)nativeCode.Pointer,
            chain = new WGPUChainedStruct
            {
                sType = WGPUSType.ShaderModuleWGSLDescriptor
            }
        };

        var shaderModuleDescriptor = new WGPUShaderModuleDescriptor
        {
            nextInChain = &descriptor.chain,
        };

        var handle = WGPU.DeviceCreateShaderModule(device.NativePointer, &shaderModuleDescriptor);
        return new GPUShaderModule(handle);
    }
}
