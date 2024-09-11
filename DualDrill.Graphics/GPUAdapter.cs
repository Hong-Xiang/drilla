namespace DualDrill.Graphics;

public partial interface IGPUAdapter : IDisposable
{
    public ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(CancellationToken cancellation);
}

public sealed partial record class GPUAdapter<TBackend>
{
    public async ValueTask<IGPUDevice> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    }

    public ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(CancellationToken cancellation)
    {
        return TBackend.Instance.RequestAdapterInfoAsync(this, cancellation);
    }
}

