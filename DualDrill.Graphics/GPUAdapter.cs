namespace DualDrill.Graphics;

public partial interface IGPUAdapter : IDisposable
{
    public ValueTask<IGPUDevice> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation);
    public ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(CancellationToken cancellation);
}

public sealed partial record class GPUAdapter<TBackend>
{
    public ValueTask<GPUDevice> RequestDeviceAsyncLegacy(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
       => TBackend.Instance.RequestDeviceAsyncLegacy(this, descriptor, cancellation);
}

public sealed partial record class GPUAdapter<TBackend>(GPUHandle<TBackend, GPUAdapter<TBackend>> Handle)
    : IDisposable, IGPUAdapter
    where TBackend : IBackend<TBackend>
{
    public async ValueTask<IGPUDevice> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }

    public ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(CancellationToken cancellation)
    {
        return TBackend.Instance.RequestAdapterInfoAsync(this, cancellation);
    }
}
