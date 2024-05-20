using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public unsafe sealed class Device(
    Silk.NET.WebGPU.WebGPU Api,
    Silk.NET.WebGPU.Device* Handle
)
{
    public Silk.NET.WebGPU.Device* Handle { get; } = Handle;

    public ShaderModule CreateShaderModule(string code)
    {
        using var codePtr = NativeStringRef.Create(code);
        var wgslDescriptor = new ShaderModuleWGSLDescriptor
        {
            Chain = new ChainedStruct
            {
                SType = SType.ShaderModuleWgslDescriptor
            },
            Code = codePtr.Handle,
        };
        var shaderDescriptor = new ShaderModuleDescriptor
        {
            NextInChain = &wgslDescriptor.Chain,
        };
        return new ShaderModule(Api, Api.DeviceCreateShaderModule(Handle, shaderDescriptor));
    }

    public RenderPipeline* CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
    {
        return Api.DeviceCreateRenderPipeline(Handle, descriptor);
    }

    public Buffer CreateBuffer(in BufferDescriptor descriptor)
    {
        return new(Api, Api.DeviceCreateBuffer(Handle, descriptor));
    }
}
