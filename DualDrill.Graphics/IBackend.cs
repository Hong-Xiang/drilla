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
}

