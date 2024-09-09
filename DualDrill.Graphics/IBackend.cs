namespace DualDrill.Graphics;

public partial interface IBackend<TBackend>
    : IDisposable
       where TBackend : IBackend<TBackend>
{
    public abstract static TBackend Instance { get; }
    internal ValueTask<GPUDevice> RequestDeviceAsyncLegacy(
        GPUAdapter<TBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    );
    internal GPUTextureView<TBackend> CreateTextureView(GPUTexture<TBackend> texture, GPUTextureViewDescriptor descriptor);

    internal void Poll(GPUDevice<TBackend> device);

    internal ValueTask PollAsync(GPUDevice<TBackend> device, CancellationToken cancellation);

    internal ValueTask<GPUAdapterInfo> RequestAdapterInfoAsync(GPUAdapter<TBackend> adapter, CancellationToken cancellation);

    internal void Present(GPUSurface<TBackend> surface);
}

