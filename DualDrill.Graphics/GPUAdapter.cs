namespace DualDrill.Graphics;

public sealed partial record class GPUAdapter<TBackend>
{
    public ValueTask<GPUDevice> RequestDeviceAsyncLegacy(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
       => TBackend.Instance.RequestDeviceAsyncLegacy(this, descriptor, cancellation);
}
