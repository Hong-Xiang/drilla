namespace DualDrill.Graphics;

public interface IGPUInstance
{
}

public sealed record class GPUInstance<TBackend>(GPUHandle<TBackend, GPUInstance<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }

    public ValueTask<GPUAdapter<TBackend>> RequestAdapterAsync(
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken)
    {
        return TBackend.Instance.RequestAdapterAsync(this, options, cancellationToken);
    }
}
