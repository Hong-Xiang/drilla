namespace DualDrill.Graphics;

public sealed record class GPUAdapter<TBackend>(GPUHandle<TBackend, GPUAdapter<TBackend>> Handle) : IDisposable
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }

    public ValueTask<GPUDevice> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
        => TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
}
