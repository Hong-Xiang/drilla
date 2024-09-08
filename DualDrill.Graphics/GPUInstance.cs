namespace DualDrill.Graphics;

public partial interface IGPUInstance : IGPUHandle
{
    public GPUTextureFormat GetPreferredCanvasFormat();
    public ValueTask<IGPUAdapter> RequestAdapterAsync(GPURequestAdapterOptions options, CancellationToken cancellation);
}

public sealed partial record class GPUInstance<TBackend>(GPUHandle<TBackend, GPUInstance<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public GPUTextureFormat GetPreferredCanvasFormat()
    {
        return TBackend.Instance.GetPreferredCanvasFormat(this);
    }

    public async ValueTask<IGPUAdapter> RequestAdapterAsync(GPURequestAdapterOptions options, CancellationToken cancellation)
    {
        return await TBackend.Instance.RequestAdapterAsync(this, options, cancellation);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}

