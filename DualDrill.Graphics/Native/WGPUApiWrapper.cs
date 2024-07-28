using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics.Native;

sealed class WGPUApiWrapper
    : INativeDisposer<WGPUInstanceImpl>
    , INativeDisposer<WGPUAdapterImpl>
    , INativeDisposer<WGPUDeviceImpl>
    , INativeDisposer<WGPUSurfaceImpl>
    , INativeDisposer<WGPUShaderModuleImpl>
    , INativeDisposer<WGPUBufferImpl>
    , INativeDisposer<WGPUTextureImpl>
    , INativeDisposer<WGPUSamplerImpl>
    , INativeDisposer<WGPUBindGroupImpl>
    , INativeDisposer<WGPUBindGroupLayoutImpl>
    , INativeDisposer<WGPUPipelineLayoutImpl>
    , INativeDisposer<WGPURenderPipelineImpl>
    , INativeDisposer<WGPUComputePipelineImpl>
    , INativeDisposer<WGPUCommandBufferImpl>
    , INativeDisposer<WGPUQueueImpl>
    , INativeDisposer<WGPUTextureViewImpl>
    , INativeDisposer<WGPUCommandEncoderImpl>
    , INativeDisposer<WGPUComputePassEncoderImpl>
    , INativeDisposer<WGPURenderPassEncoderImpl>
    , INativeDisposer<WGPURenderBundleImpl>
    , INativeDisposer<WGPURenderBundleEncoderImpl>
    , INativeDisposer<WGPUQuerySetImpl>
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
