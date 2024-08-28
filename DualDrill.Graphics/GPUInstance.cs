using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;

public interface IGPUInstance
{
}

public sealed partial class GPUInstance : IDisposable
{
    public unsafe GPUInstance()
    {
        WGPUInstanceDescriptor descriptor = new();
        var pointer = WGPU.CreateInstance(&descriptor);
        Handle = new(pointer);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestAdaptorCallback(GPURequestAdapterStatus status, WGPUAdapterImpl* adapter, sbyte* message, void* data)
    {
        RequestCallback<WGPUNativeApiInterop, WGPUAdapterImpl, GPURequestAdapterStatus>.Callback(status, adapter, message, data);
    }

    public unsafe GPUAdapter RequestAdapter(GPUSurface? surface)
    {
        var options = new WGPURequestAdapterOptions
        {
            powerPreference = GPUPowerPreference.HighPerformance
        };
        if (surface is not null)
        {
            options.compatibleSurface = surface.Handle;
        }
        var result = new RequestCallbackResult<WGPUAdapterImpl, GPURequestAdapterStatus>();
        WGPU.InstanceRequestAdapter(
            Handle,
            &options,
            &RequestAdaptorCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUAdapter)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        return new GPUAdapter(result.Handle);
    }
}
