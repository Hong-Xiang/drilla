using Silk.NET.WebGPU;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics;

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
