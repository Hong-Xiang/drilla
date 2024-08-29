namespace DualDrill.Graphics;

public interface IBackend<TBackend>
    : IGPUHandleDisposer<TBackend, GPUInstance<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUAdapter<TBackend>>
    where TBackend : IBackend<TBackend>
{
    public abstract static TBackend Instance { get; }
    internal ValueTask<GPUAdapter<TBackend>> RequestAdapterAsync(
        GPUInstance<TBackend> instance,
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken);

    internal ValueTask<GPUDevice> RequestDeviceAsync(
        GPUAdapter<TBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    );

    // GPUInstance
    //ValueTask<GPUAdaptor<TBackend>> RequestAdapterAsync(GPUInstance<TBackend> instance, GPURequestAdapterOptions options, CancellationToken cancellation);
    //GPUTextureFormat GetPreferredCanvasFormat(GPUInstance<TBackend> instance);

    // GPUAdapter

    // GPUSurface/GPUCanvasContext
    //void Present(GPUSurface<TBackend> surface);
}

//public static class GraphicsApiExtension
//{
//    public static GPUInstance<TApi, THandle> CreateInstance<TApi, THandle>(this TApi api)
//        where TApi : IGraphicsApi<TApi, THandle>
//    {
//        var handle = api.CreateInstance();
//        if (!api.IsValidHandle(handle))
//        {
//            throw new GraphicsApiException("Failed to create instance");
//        }
//        return new GPUInstance<TApi, THandle>(api, handle);
//    }
//}

public readonly record struct GPUHandle<TBackend, TResource>(nint Pointer, object? Data = null)
    where TBackend : IBackend<TBackend>
{
}

public interface IGPUHandleDisposer<TBackend, TResource>
    where TBackend : IBackend<TBackend>, IGPUHandleDisposer<TBackend, TResource>
{
    internal void DisposeHandle(GPUHandle<TBackend, TResource> handle);
}
