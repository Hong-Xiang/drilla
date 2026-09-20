namespace DualDrill.Graphics;


public partial interface IGPUTextureView : IDisposable
{

}

public sealed partial record class GPUTextureView<TBackend>(GPUHandle<TBackend, GPUTextureView<TBackend>> Handle)
    : IDisposable, IGPUTextureView
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        Handle.Release(TBackend.Instance.DisposeHandle);
    }
}



