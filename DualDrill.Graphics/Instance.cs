using Silk.NET.WebGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public unsafe sealed class Instance(Silk.NET.WebGPU.WebGPU Api, Silk.NET.WebGPU.Instance* Handle) : IDisposable
{
    public Silk.NET.WebGPU.Instance* Handle { get; } = Handle;

    private bool disposedValue;

    public static Instance Create(InstanceDescriptor descriptor)
    {
        var handle = WebGPUApi.API.CreateInstance(in descriptor);
        if (handle is null)
        {
            throw new Exception("Failed to create instance");
        }
        return new(WebGPUApi.API, handle);
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


    public Adapter RequestAdapter(in RequestAdapterOptions options)
    {
        var result = new RequestResult();
        Api.InstanceRequestAdapter(Handle, in options, Callback, ref result);
        if (result.Adapter is null)
        {
            throw new NullReferenceException("Failed to get adapter");
        }
        return new Adapter(Api, result.Adapter);
    }


    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                WebGPUApi.API.InstanceRelease(Handle);
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
