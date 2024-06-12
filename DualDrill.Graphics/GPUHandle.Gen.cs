using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
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
    internal NativeHandle<WGPUApiWrapper, WGPUAdapterImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUDeviceImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUBufferImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUTextureImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUTextureViewImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUSamplerImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUBindGroupLayoutImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUBindGroupImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUPipelineLayoutImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUShaderModuleImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUComputePipelineImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPURenderPipelineImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUCommandBufferImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUCommandEncoderImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUComputePassEncoderImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPURenderPassEncoderImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPURenderBundleImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPURenderBundleEncoderImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUQueueImpl> Handle { get; }
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
    internal NativeHandle<WGPUApiWrapper, WGPUQuerySetImpl> Handle { get; }
    internal unsafe GPUQuerySet(WGPUQuerySetImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUQuerySetImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUInstanceW : IDisposable
{
    internal NativeHandle<WGPUApiWrapper, WGPUInstanceImpl> Handle { get; }
    internal unsafe GPUInstanceW(WGPUInstanceImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUInstanceImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }
}
public sealed partial class GPUSurface : IDisposable
{
    internal NativeHandle<WGPUApiWrapper, WGPUSurfaceImpl> Handle { get; }
    internal unsafe GPUSurface(WGPUSurfaceImpl* handle)
    {
        Handle = new(handle);
    }

    public unsafe WGPUSurfaceImpl* NativePointer => Handle; public void Dispose()
    {
        Handle.Dispose();
    }

    public unsafe GPUTextureFormat PreferredFormat(GPUAdapter adapter)
    {
        var result = WGPU.wgpuSurfaceGetPreferredFormat(Handle, adapter.Handle);
        return (GPUTextureFormat)result;
    }

    public unsafe void Present()
    {
        WGPU.wgpuSurfacePresent(Handle);
    }

    public static unsafe GPUSurface Create(INativeWindowSource view, GPUInstanceW instance)
    {
        WGPUSurfaceDescriptor descriptor = new();
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND = new();
        surfaceDescriptorFromWindowsHWND.chain = new WGPUChainedStruct
        {
            next = null,
            sType = WGPUSType.WGPUSType_SurfaceDescriptorFromWindowsHWND
        };
        surfaceDescriptorFromWindowsHWND.hwnd = (void*)(((IntPtr, IntPtr, IntPtr)?)view.Native.Win32).Value.Item1;
        surfaceDescriptorFromWindowsHWND.hinstance = (void*)(((IntPtr, IntPtr, IntPtr)?)view.Native.Win32).Value.Item3;
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND2 = surfaceDescriptorFromWindowsHWND;
        descriptor.nextInChain = (WGPUChainedStruct*)(&surfaceDescriptorFromWindowsHWND2);
        WGPUSurfaceImpl* result = WGPU.wgpuInstanceCreateSurface(instance.Handle, &descriptor);
        if (result is null)
        {
            throw new GraphicsApiException("Failed to create surface");
        }
        return new(result);
    }
}
