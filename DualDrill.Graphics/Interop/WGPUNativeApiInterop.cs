using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics.Interop;

sealed class WGPUNativeApiInterop
    : INativeApiDisposer<WGPUInstanceImpl>
    , INativeApiDisposer<WGPUAdapterImpl>
    , INativeApiDisposer<WGPUDeviceImpl>
    , INativeApiDisposer<WGPUSurfaceImpl>
    , INativeApiDisposer<WGPUShaderModuleImpl>
    , INativeApiDisposer<WGPUBufferImpl>
    , INativeApiDisposer<WGPUTextureImpl>
    , INativeApiDisposer<WGPUSamplerImpl>
    , INativeApiDisposer<WGPUBindGroupImpl>
    , INativeApiDisposer<WGPUBindGroupLayoutImpl>
    , INativeApiDisposer<WGPUPipelineLayoutImpl>
    , INativeApiDisposer<WGPURenderPipelineImpl>
    , INativeApiDisposer<WGPUComputePipelineImpl>
    , INativeApiDisposer<WGPUCommandBufferImpl>
    , INativeApiDisposer<WGPUQueueImpl>
    , INativeApiDisposer<WGPUTextureViewImpl>
    , INativeApiDisposer<WGPUCommandEncoderImpl>
    , INativeApiDisposer<WGPUComputePassEncoderImpl>
    , INativeApiDisposer<WGPURenderPassEncoderImpl>
    , INativeApiDisposer<WGPURenderBundleImpl>
    , INativeApiDisposer<WGPURenderBundleEncoderImpl>
    , INativeApiDisposer<WGPUQuerySetImpl>
    , IStatusIsSuccessApi<GPURequestAdapterStatus>
    , IStatusIsSuccessApi<GPURequestDeviceStatus>
{
    public static bool IsSuccess(GPURequestAdapterStatus status)
    {
        return status == GPURequestAdapterStatus.Success;
    }

    public static bool IsSuccess(GPURequestDeviceStatus status)
    {
        return status == GPURequestDeviceStatus.Success;
    }

    public static unsafe void NativeDispose(WGPUInstanceImpl* handle)
    {
        WGPU.InstanceRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUAdapterImpl* handle)
    {
        WGPU.AdapterRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUSurfaceImpl* handle)
    {
        WGPU.SurfaceRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUDeviceImpl* handle)
    {
        WGPU.DeviceRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUShaderModuleImpl* handle)
    {
        WGPU.ShaderModuleRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUBufferImpl* handle)
    {
        WGPU.BufferRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUTextureImpl* handle)
    {
        WGPU.TextureRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUSamplerImpl* handle)
    {
        WGPU.SamplerRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUBindGroupImpl* handle)
    {
        WGPU.BindGroupRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUBindGroupLayoutImpl* handle)
    {
        WGPU.BindGroupLayoutRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUPipelineLayoutImpl* handle)
    {
        WGPU.PipelineLayoutRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderPipelineImpl* handle)
    {
        WGPU.RenderPipelineRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUComputePipelineImpl* handle)
    {
        WGPU.ComputePipelineRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUCommandBufferImpl* handle)
    {
        WGPU.CommandBufferRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUQueueImpl* handle)
    {
        WGPU.QueueRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUTextureViewImpl* handle)
    {
        WGPU.TextureViewRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUCommandEncoderImpl* handle)
    {
        WGPU.CommandEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUComputePassEncoderImpl* handle)
    {
        WGPU.ComputePassEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderPassEncoderImpl* handle)
    {
        WGPU.RenderPassEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderBundleImpl* handle)
    {
        WGPU.RenderBundleRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderBundleEncoderImpl* handle)
    {
        WGPU.RenderBundleEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUQuerySetImpl* handle)
    {
        WGPU.QuerySetRelease(handle);
    }
}
