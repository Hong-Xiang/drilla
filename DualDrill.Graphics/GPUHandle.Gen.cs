using DualDrill.Graphics.Interop;
using Silk.NET.Core.Contexts;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUAdapter : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUAdapterImpl> Handle { get; }
    internal unsafe GPUAdapter(WGPUAdapterImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUAdapterImpl* NativePointer => Handle;
    public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUDevice : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUDeviceImpl> Handle { get; }
    internal unsafe GPUDevice(WGPUDeviceImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUDeviceImpl* NativePointer => Handle;
    public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUBuffer : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUBufferImpl> Handle { get; }
    internal unsafe GPUBuffer(WGPUBufferImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUBufferImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUTexture : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUTextureImpl> Handle { get; }
    internal unsafe GPUTexture(WGPUTextureImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUTextureImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUTextureView : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUTextureViewImpl> Handle { get; }
    internal unsafe GPUTextureView(WGPUTextureViewImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUTextureViewImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUSampler : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUSamplerImpl> Handle { get; }
    internal unsafe GPUSampler(WGPUSamplerImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUSamplerImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUBindGroupLayout : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUBindGroupLayoutImpl> Handle { get; }
    internal unsafe GPUBindGroupLayout(WGPUBindGroupLayoutImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUBindGroupLayoutImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUBindGroup : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUBindGroupImpl> Handle { get; }
    internal unsafe GPUBindGroup(WGPUBindGroupImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUBindGroupImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUPipelineLayout : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUPipelineLayoutImpl> Handle { get; }
    internal unsafe GPUPipelineLayout(WGPUPipelineLayoutImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUPipelineLayoutImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUShaderModule : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUShaderModuleImpl> Handle { get; }
    internal unsafe GPUShaderModule(WGPUShaderModuleImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUShaderModuleImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUComputePipeline : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUComputePipelineImpl> Handle { get; }
    internal unsafe GPUComputePipeline(WGPUComputePipelineImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUComputePipelineImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPURenderPipeline : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPURenderPipelineImpl> Handle { get; }
    internal unsafe GPURenderPipeline(WGPURenderPipelineImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPURenderPipelineImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUCommandBuffer : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUCommandBufferImpl> Handle { get; }
    internal unsafe GPUCommandBuffer(WGPUCommandBufferImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUCommandBufferImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUCommandEncoder : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUCommandEncoderImpl> Handle { get; }
    internal unsafe GPUCommandEncoder(WGPUCommandEncoderImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUCommandEncoderImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUComputePassEncoder : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUComputePassEncoderImpl> Handle { get; }
    internal unsafe GPUComputePassEncoder(WGPUComputePassEncoderImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUComputePassEncoderImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPURenderPassEncoder : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPURenderPassEncoderImpl> Handle { get; }
    internal unsafe GPURenderPassEncoder(WGPURenderPassEncoderImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPURenderPassEncoderImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPURenderBundle : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPURenderBundleImpl> Handle { get; }
    internal unsafe GPURenderBundle(WGPURenderBundleImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPURenderBundleImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPURenderBundleEncoder : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPURenderBundleEncoderImpl> Handle { get; }
    internal unsafe GPURenderBundleEncoder(WGPURenderBundleEncoderImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPURenderBundleEncoderImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUQueue : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUQueueImpl> Handle { get; }
    internal unsafe GPUQueue(WGPUQueueImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUQueueImpl* NativePointer => Handle;
    public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUQuerySet : IDisposable
{
    internal NativeHandle<WGPUNativeApiInterop, WGPUQuerySetImpl> Handle { get; }
    internal unsafe GPUQuerySet(WGPUQuerySetImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUQuerySetImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
