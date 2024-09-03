using DualDrill.Graphics.Interop;
using DualDrill.Interop;

namespace DualDrill.Graphics;

public sealed partial class GPUShaderModule
{
    internal unsafe static GPUShaderModule Create(GPUDevice device, string code)
    {
        var codeUtf8 = InteropUtf8String.Create(code);
        using var nativeCode = codeUtf8.Pin();
        var descriptor = new WGPUShaderModuleWGSLDescriptor
        {
            code = nativeCode.Pointer,
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
