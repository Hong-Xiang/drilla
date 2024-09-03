using Evergine.Bindings.WebGPU;
namespace DualDrill.Graphics.Backend;
using static Evergine.Bindings.WebGPU.WebGPUNative;
using Backend = DualDrill.Graphics.Backend.WebGPUNETBackend;


public sealed partial class WebGPUNETBackend
{
    void IGPUHandleDisposer<Backend, GPUAdapter<Backend>>.DisposeHandle(GPUHandle<Backend, GPUAdapter<Backend>> handle)
    {
        wgpuAdapterRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUBindGroup<Backend>>.DisposeHandle(GPUHandle<Backend, GPUBindGroup<Backend>> handle)
    {
        wgpuBindGroupRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUBindGroupLayout<Backend>>.DisposeHandle(GPUHandle<Backend, GPUBindGroupLayout<Backend>> handle)
    {
        wgpuBindGroupLayoutRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUBuffer<Backend>>.DisposeHandle(GPUHandle<Backend, GPUBuffer<Backend>> handle)
    {
        wgpuBufferRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUCommandBuffer<Backend>>.DisposeHandle(GPUHandle<Backend, GPUCommandBuffer<Backend>> handle)
    {
        wgpuCommandBufferRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUCommandEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPUCommandEncoder<Backend>> handle)
    {
        wgpuCommandEncoderRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUComputePassEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPUComputePassEncoder<Backend>> handle)
    {
        wgpuComputePassEncoderRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUComputePipeline<Backend>>.DisposeHandle(GPUHandle<Backend, GPUComputePipeline<Backend>> handle)
    {
        wgpuComputePipelineRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUDevice<Backend>>.DisposeHandle(GPUHandle<Backend, GPUDevice<Backend>> handle)
    {
        wgpuDeviceRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUInstance<Backend>>.DisposeHandle(GPUHandle<Backend, GPUInstance<Backend>> handle)
    {
        wgpuInstanceRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUPipelineLayout<Backend>>.DisposeHandle(GPUHandle<Backend, GPUPipelineLayout<Backend>> handle)
    {
        wgpuPipelineLayoutRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUQuerySet<Backend>>.DisposeHandle(GPUHandle<Backend, GPUQuerySet<Backend>> handle)
    {
        wgpuQuerySetRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUQueue<Backend>>.DisposeHandle(GPUHandle<Backend, GPUQueue<Backend>> handle)
    {
        wgpuQueueRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPURenderBundle<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderBundle<Backend>> handle)
    {
        wgpuRenderBundleRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPURenderBundleEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderBundleEncoder<Backend>> handle)
    {
        wgpuRenderBundleEncoderRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPURenderPassEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderPassEncoder<Backend>> handle)
    {
        wgpuRenderPassEncoderRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPURenderPipeline<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderPipeline<Backend>> handle)
    {
        wgpuRenderPipelineRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUSampler<Backend>>.DisposeHandle(GPUHandle<Backend, GPUSampler<Backend>> handle)
    {
        wgpuSamplerRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUShaderModule<Backend>>.DisposeHandle(GPUHandle<Backend, GPUShaderModule<Backend>> handle)
    {
        wgpuShaderModuleRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUSurface<Backend>>.DisposeHandle(GPUHandle<Backend, GPUSurface<Backend>> handle)
    {
        wgpuSurfaceRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUTexture<Backend>>.DisposeHandle(GPUHandle<Backend, GPUTexture<Backend>> handle)
    {
        wgpuTextureRelease(handle.ToNative());
    }
    void IGPUHandleDisposer<Backend, GPUTextureView<Backend>>.DisposeHandle(GPUHandle<Backend, GPUTextureView<Backend>> handle)
    {
        wgpuTextureViewRelease(handle.ToNative());
    }
}

public static partial class WebGPUNetBackendExtensions
{
    public static WGPUAdapter ToNative(this GPUHandle<Backend, GPUAdapter<Backend>> instance)
    => new(instance.Pointer);
    public static WGPUBindGroup ToNative(this GPUHandle<Backend, GPUBindGroup<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUBindGroupLayout ToNative(this GPUHandle<Backend, GPUBindGroupLayout<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUBuffer ToNative(this GPUHandle<Backend, GPUBuffer<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUCommandBuffer ToNative(this GPUHandle<Backend, GPUCommandBuffer<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUCommandEncoder ToNative(this GPUHandle<Backend, GPUCommandEncoder<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUComputePassEncoder ToNative(this GPUHandle<Backend, GPUComputePassEncoder<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUComputePipeline ToNative(this GPUHandle<Backend, GPUComputePipeline<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUDevice ToNative(this GPUHandle<Backend, GPUDevice<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUInstance ToNative(this GPUHandle<Backend, GPUInstance<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUPipelineLayout ToNative(this GPUHandle<Backend, GPUPipelineLayout<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUQuerySet ToNative(this GPUHandle<Backend, GPUQuerySet<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUQueue ToNative(this GPUHandle<Backend, GPUQueue<Backend>> instance)
        => new(instance.Pointer);
    public static WGPURenderBundle ToNative(this GPUHandle<Backend, GPURenderBundle<Backend>> instance)
        => new(instance.Pointer);
    public static WGPURenderBundleEncoder ToNative(this GPUHandle<Backend, GPURenderBundleEncoder<Backend>> instance)
        => new(instance.Pointer);
    public static WGPURenderPassEncoder ToNative(this GPUHandle<Backend, GPURenderPassEncoder<Backend>> instance)
        => new(instance.Pointer);
    public static WGPURenderPipeline ToNative(this GPUHandle<Backend, GPURenderPipeline<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUSampler ToNative(this GPUHandle<Backend, GPUSampler<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUShaderModule ToNative(this GPUHandle<Backend, GPUShaderModule<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUSurface ToNative(this GPUHandle<Backend, GPUSurface<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUTexture ToNative(this GPUHandle<Backend, GPUTexture<Backend>> instance)
        => new(instance.Pointer);
    public static WGPUTextureView ToNative(this GPUHandle<Backend, GPUTextureView<Backend>> instance)
        => new(instance.Pointer);
}
