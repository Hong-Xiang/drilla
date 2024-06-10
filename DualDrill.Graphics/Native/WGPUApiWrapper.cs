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
    , IStatusIsSuccessApi<WGPURequestAdapterStatus>
    , IStatusIsSuccessApi<WGPURequestDeviceStatus>
{
    public static bool IsSuccess(WGPURequestAdapterStatus status)
    {
        return status == WGPURequestAdapterStatus.WGPURequestAdapterStatus_Success;
    }

    public static bool IsSuccess(WGPURequestDeviceStatus status)
    {
        return status == WGPURequestDeviceStatus.WGPURequestDeviceStatus_Success;
    }

    public static unsafe void NativeDispose(WGPUInstanceImpl* handle)
    {
        WGPU.wgpuInstanceRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUAdapterImpl* handle)
    {
        WGPU.wgpuAdapterRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUSurfaceImpl* handle)
    {
        WGPU.wgpuSurfaceRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUDeviceImpl* handle)
    {
        WGPU.wgpuDeviceRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUShaderModuleImpl* handle)
    {
        WGPU.wgpuShaderModuleRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUBufferImpl* handle)
    {
        WGPU.wgpuBufferRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUTextureImpl* handle)
    {
        WGPU.wgpuTextureRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUSamplerImpl* handle)
    {
        WGPU.wgpuSamplerRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUBindGroupImpl* handle)
    {
        WGPU.wgpuBindGroupRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUBindGroupLayoutImpl* handle)
    {
        WGPU.wgpuBindGroupLayoutRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUPipelineLayoutImpl* handle)
    {
        WGPU.wgpuPipelineLayoutRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderPipelineImpl* handle)
    {
        WGPU.wgpuRenderPipelineRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUComputePipelineImpl* handle)
    {
        WGPU.wgpuComputePipelineRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUCommandBufferImpl* handle)
    {
        WGPU.wgpuCommandBufferRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUQueueImpl* handle)
    {
        WGPU.wgpuQueueRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUTextureViewImpl* handle)
    {
        WGPU.wgpuTextureViewRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUCommandEncoderImpl* handle)
    {
        WGPU.wgpuCommandEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUComputePassEncoderImpl* handle)
    {
        WGPU.wgpuComputePassEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderPassEncoderImpl* handle)
    {
        WGPU.wgpuRenderPassEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderBundleImpl* handle)
    {
        WGPU.wgpuRenderBundleRelease(handle);
    }

    public static unsafe void NativeDispose(WGPURenderBundleEncoderImpl* handle)
    {
        WGPU.wgpuRenderBundleEncoderRelease(handle);
    }

    public static unsafe void NativeDispose(WGPUQuerySetImpl* handle)
    {
        WGPU.wgpuQuerySetRelease(handle);
    }
}
