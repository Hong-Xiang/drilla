using Silk.NET.WebGPU;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics;


//interface IGPUInstance<TApi> : IDisposable
//    where TApi : IGraphicsApi<TApi>
//{
//    void IDisposable.Dispose()
//    {
//    }
//}

//public sealed class GPUInstance<TApi, THandle> : IDisposable
//    where TApi : IGraphicsApi<TApi, THandle>
//{
//    internal GPUInstance(TApi api, THandle handle)
//    {
//        Api = api;
//        Handle = handle;
//    }

//    internal THandle Handle { get; }
//    internal TApi Api { get; }

//    public void Dispose() { Api.DestroyInstance(Handle); }
//    public GPUAdapter<TApi, THandle> RequestAdapter()
//    {
//        var handle = Api.InstanceRequestAdapter(Handle);
//        return new(Api, handle);
//    }
//}


public unsafe sealed class Instance(Silk.NET.WebGPU.WebGPU Api, Silk.NET.WebGPU.Instance* Handle) : IDisposable
{
    internal readonly static Silk.NET.WebGPU.WebGPU Api = Silk.NET.WebGPU.WebGPU.GetApi();
    public Silk.NET.WebGPU.Instance* Handle { get; } = Handle;

    private bool disposedValue;

    public static Instance Create(InstanceDescriptor descriptor)
    {
        //var handle = WebGPUGraphicsApi.Api.CreateInstance(in descriptor);
        //if (handle is null)
        //{
        //    throw new Exception("Failed to create instance");
        //}
        //return new(WebGPUGraphicsApi.Api, handle);
        var desc = new InstanceDescriptor();
        var result = Api.CreateInstance(in desc);
        if (result is null)
        {
            throw new NullReferenceException("Failed to create Instance");
        }
        return new Instance(Api, result);
    }

    struct RequestResult
    {
        public Silk.NET.WebGPU.Adapter* Adapter;
    }

    readonly PfnRequestAdapterCallback Callback = new((status, adapter, message, data) =>
        {
            var resultData = (RequestResult*)data;
            if (status == RequestAdapterStatus.Success && resultData->Adapter is null)
            {
                resultData->Adapter = adapter;
            }
            else
            {
                Console.WriteLine(Marshal.PtrToStringUTF8((nint)message));
            }
        });


 


    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Api.InstanceRelease(Handle);
                // TODO: dispose managed state (managed objects)
            }


            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }


    // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~Instance()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
