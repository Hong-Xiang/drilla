using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics.Native;

sealed class WGPUDisposer
    : INativeDisposer<WGPUInstanceImpl>
    , INativeDisposer<WGPUAdapterImpl>
    , INativeDisposer<WGPUSurfaceImpl>
    , IStatusIsSuccessApi<WGPURequestAdapterStatus>
{
    public static bool IsSuccess(WGPURequestAdapterStatus status)
    {
        return status == WGPURequestAdapterStatus.WGPURequestAdapterStatus_Success;
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
}
