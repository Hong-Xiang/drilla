using DualDrill.Graphics.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics.Backend;

public static class WebGPUApiExtension
{
    public static GPUShaderModule CreateShaderModule(this GPUDevice device, string code) => GPUShaderModule.Create(device, code);
}

public sealed class WGPUBackend : IBackend<WGPUBackend>
{
    public static WGPUBackend Instance { get; } = new();

    unsafe public GPUInstance<WGPUBackend> CreateGPUInstance()
    {
        WGPUInstanceDescriptor descriptor = new();
        var pointer = WGPU.CreateInstance(&descriptor);
        return new(new((nint)pointer));
    }
    public static unsafe GPUSurface CreateSurface(Silk.NET.Windowing.IView view, GPUInstance<WGPUBackend> instance)
    {
        WGPUSurfaceDescriptor descriptor = new();
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND = new();
        surfaceDescriptorFromWindowsHWND.chain = new WGPUChainedStruct
        {
            next = null,
            sType = WGPUSType.SurfaceDescriptorFromWindowsHWND
        };
        surfaceDescriptorFromWindowsHWND.hwnd = (void*)(((nint, nint, nint)?)view.Native.Win32).Value.Item1;
        surfaceDescriptorFromWindowsHWND.hinstance = (void*)(((nint, nint, nint)?)view.Native.Win32).Value.Item3;
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND2 = surfaceDescriptorFromWindowsHWND;
        descriptor.nextInChain = (WGPUChainedStruct*)&surfaceDescriptorFromWindowsHWND;
        WGPUSurfaceImpl* result = WGPU.InstanceCreateSurface(ToNativePointer(instance.Handle), &descriptor);
        if (result is null)
        {
            throw new GraphicsApiException("Failed to create surface");
        }
        return new(result);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestAdaptorCallback(
        GPURequestAdapterStatus status,
        WGPUAdapterImpl* adapter,
        sbyte* message,
        void* data)
    {
        RequestCallback<WGPUNativeApiInterop, WGPUAdapterImpl, GPURequestAdapterStatus>.Callback(status, adapter, message, data);
    }

    unsafe ValueTask<GPUAdapter<WGPUBackend>> IBackend<WGPUBackend>.RequestAdapterAsync(
          GPUInstance<WGPUBackend> instance,
          GPURequestAdapterOptions options,
          CancellationToken cancellation)
    {
        var options_ = new WGPURequestAdapterOptions
        {
            powerPreference = options.PowerPreference,
            forceFallbackAdapter = (GPUBool)options.ForceFallbackAdapter
        };
        //if (options.CompatibleSurface is GPUSurface surface)
        //{
        //    options_.compatibleSurface = ToNativePointer(surface.Handle);
        //}
        var result = new RequestCallbackResult<WGPUAdapterImpl, GPURequestAdapterStatus>();
        WGPU.InstanceRequestAdapter(
            ToNativePointer(instance.Handle),
            &options_,
            &RequestAdaptorCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUAdapter<WGPUBackend>)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        return ValueTask.FromResult(new GPUAdapter<WGPUBackend>(new((nint)result.Handle)));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestDeviceCallback(GPURequestDeviceStatus status, WGPUDeviceImpl* device, sbyte* message, void* data)
    {
        RequestCallback<WGPUNativeApiInterop, WGPUDeviceImpl, GPURequestDeviceStatus>.Callback(status, device, message, data);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void DeviceUncapturedErrorCallback(GPUErrorType errorType, sbyte* message, void* data)
    {
        Console.Error.WriteLine($"Device uncaptured error type = {Enum.GetName(errorType)}, message {Marshal.PtrToStringUTF8((nint)message)}");
    }


    unsafe ValueTask<GPUDevice> IBackend<WGPUBackend>.RequestDeviceAsync(
        GPUAdapter<WGPUBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    )
    {
        WGPUDeviceDescriptor descriptor_ = new();
        var result = new RequestCallbackResult<WGPUDeviceImpl, GPURequestDeviceStatus>();
        WGPU.AdapterRequestDevice(
            ToNativePointer(adapter.Handle),
            &descriptor_,
            &RequestDeviceCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUDevice)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        WGPU.DeviceSetUncapturedErrorCallback(result.Handle, &DeviceUncapturedErrorCallback, null);
        return ValueTask.FromResult(new GPUDevice(result.Handle));
    }

    void IGPUHandleDisposer<WGPUBackend, GPUAdapter<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUAdapter<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    private static unsafe WGPUInstanceImpl* ToNativePointer(GPUHandle<WGPUBackend, GPUInstance<WGPUBackend>> handle) => (WGPUInstanceImpl*)handle.Pointer;
    unsafe void IGPUHandleDisposer<WGPUBackend, GPUInstance<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUInstance<WGPUBackend>> handle)
    {
        WGPU.InstanceRelease(ToNativePointer(handle));
    }
    private static unsafe WGPUAdapterImpl* ToNativePointer(GPUHandle<WGPUBackend, GPUAdapter<WGPUBackend>> handle) => (WGPUAdapterImpl*)handle.Pointer;
}

//public unsafe sealed class WebGPUGraphicsApi : IGraphicsApi<WebGPUGraphicsApi, nint>, IDisposable
//{
//    internal readonly static Silk.NET.WebGPU.WebGPU Api = Silk.NET.WebGPU.WebGPU.GetApi();

//    public readonly static WebGPUGraphicsApi Instance = new();
//    internal WebGPUGraphicsApi() { }

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.CreateInstance()
//    {
//        var desc = new Silk.NET.WebGPU.InstanceDescriptor();
//        var result = Api.CreateInstance(&desc);
//        if (result is null)
//        {
//            throw new GraphicsApiException("Failed to create GPUInstance using WebGPU Graphics API");
//        }
//        return ValueTask.FromResult((nint)result);
//    }
//    ValueTask IGraphicsApi<WebGPUGraphicsApi, nint>.DestroyInstance(nint instance)
//    {
//        Api.InstanceRelease((Silk.NET.WebGPU.Instance*)instance);
//        return ValueTask.CompletedTask;
//    }
//    bool IGraphicsApi<WebGPUGraphicsApi, nint>.IsValidHandle(nint handle) => (void*)handle is not null;
//    ref struct RequestAdapterResult
//    {
//        public Silk.NET.WebGPU.Adapter* Adapter;
//        public string? Message;
//    }

//    readonly PfnRequestAdapterCallback RequestAdapterCallback = new((status, adapter, message, data) =>
//        {
//            var resultData = (RequestAdapterResult*)data;
//            if (status == RequestAdapterStatus.Success && resultData->Adapter is null)
//            {
//                resultData->Adapter = adapter;
//            }
//            else
//            {
//                resultData->Message = Marshal.PtrToStringUTF8((nint)message);
//            }
//        });

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.InstanceRequestAdapter(nint instance)
//    {
//        var result = new RequestAdapterResult();
//        var options = new RequestAdapterOptions()
//        {
//            PowerPreference = PowerPreference.HighPerformance
//        };
//        Api.InstanceRequestAdapter((Silk.NET.WebGPU.Instance*)instance, in options, RequestAdapterCallback, &result);
//        if (result.Adapter is null)
//        {
//            throw new GraphicsApiException($"Failed to create adapter, {result.Message}");
//        }
//        return ValueTask.FromResult((nint)result.Adapter);
//    }

//    ValueTask IGraphicsApi<WebGPUGraphicsApi, nint>.DestroyAdapter(nint instance)
//    {
//        return ValueTask.CompletedTask;
//    }

//    public void Dispose()
//    {
//        RequestAdapterCallback.Dispose();
//    }

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.AdapterRequestDevice(nint adapter)
//    {
//        throw new NotImplementedException();
//    }

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.DeviceRequestQueue(nint device)
//    {
//        throw new NotImplementedException();
//    }
//}

//public unsafe static class WebGPUGraphicsApiExtension
//{
//    public static Silk.NET.WebGPU.Instance* NativeHandle(this GPUInstance<WebGPUGraphicsApi, nint> instance) => (Silk.NET.WebGPU.Instance*)instance.Handle;
//    public static Silk.NET.WebGPU.Adapter* NativeHandle(this GPUAdapter<WebGPUGraphicsApi, nint> adapter) => (Silk.NET.WebGPU.Instance*)instance.Handle;
//}
