namespace DualDrill.Graphics;

public sealed partial record class GPUAdapter<TBackend>
{
    //public ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    //=> TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    //    public ValueTask<GPUDevice> RequestDeviceAsync(

    //public ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    //{
    //    return TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    //}


    // GPUDeviceDescriptor descriptor
    //, CancellationToken cancellation
    //{
    //        return TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    //    }

    public ValueTask<GPUDevice> RequestDeviceAsyncLegacy(GPUDeviceDescriptor descriptor, CancellationToken cancellation)
        => TBackend.Instance.RequestDeviceAsyncLegacy(this, descriptor, cancellation);
}
