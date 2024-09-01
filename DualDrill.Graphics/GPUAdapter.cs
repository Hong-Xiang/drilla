namespace DualDrill.Graphics;

public sealed record class GPUAdapter<TBackend>(GPUHandle<TBackend, GPUAdapter<TBackend>> Handle) : IDisposable
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }

    public ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
        => TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    public ValueTask<GPUDevice> RequestDeviceAsyncLegacy(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
        => TBackend.Instance.RequestDeviceAsyncLegacy(this, descriptor, cancellation);
}
