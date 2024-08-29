namespace DualDrill.GPU;

public interface IBackend<TBackend>
    where TBackend : IBackend<TBackend>
{
    abstract static TBackend BackendInstance { get; }
    GPUInstance<TBackend> CreateGPUInstance();

    // GPUInstance
    ValueTask<GPUAdaptor<TBackend>> RequestAdapterAsync(GPUInstance<TBackend> instance, GPURequestAdapterOptions options, CancellationToken cancellation);
    GPUTextureFormat GetPreferredCanvasFormat(GPUInstance<TBackend> instance);

    // GPUAdapter

    // GPUSurface/GPUCanvasContext
    void Present(GPUSurface<TBackend> surface);
}
