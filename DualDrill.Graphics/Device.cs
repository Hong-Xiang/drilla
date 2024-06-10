using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUDevice
{
    public unsafe GPUCommandEncoder CreateCommandEncoder(GPUCommandEncoderDescriptor descriptor)
    {
        if (descriptor.Label is not null)
        {
            throw new NotImplementedException();
        }
        WGPUCommandEncoderDescriptor nativeDescriptor = new()
        {
        };
        return new(WGPU.wgpuDeviceCreateCommandEncoder(Handle, &nativeDescriptor));
    }

    public unsafe GPUQueue GetQueue()
    {
        return new(WGPU.wgpuDeviceGetQueue(Handle));
    }

    public unsafe GPUPipelineLayout CreatePipelineLayout(GPUPipelineLayoutDescriptor descriptor)
    {
        WGPUPipelineLayoutDescriptor native = default;
        return new GPUPipelineLayout(WGPU.wgpuDeviceCreatePipelineLayout(Handle, &native));
    }
}

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
        return new ShaderModule(Api, Api.DeviceCreateShaderModule(Handle, &shaderDescriptor));
    }

    public RenderPipeline* CreateRenderPipeline(in RenderPipelineDescriptor descriptor)
    {
        return Api.DeviceCreateRenderPipeline(Handle, in descriptor);
    }

    public Buffer CreateBuffer(in BufferDescriptor descriptor)
    {
        return new(Api, Api.DeviceCreateBuffer(Handle, in descriptor));
    }
}
