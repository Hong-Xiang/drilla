namespace DualDrill.Graphics;

public partial interface IGPUAdapter : IDisposable
{
    public ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(CancellationToken cancellation);
    public ValueTask<IGPUDevice> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation);
}

public sealed partial record class GPUAdapter<TBackend>(GPUHandle<TBackend, GPUAdapter<TBackend>> Handle)
    : IDisposable, IGPUAdapter
    where TBackend : IBackend<TBackend>
{
    public async ValueTask<IGPUDevice> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    }

    public ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(CancellationToken cancellation)
    {
        return TBackend.Instance.RequestAdapterInfoAsync(this, cancellation);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}


