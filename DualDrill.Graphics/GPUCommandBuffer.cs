namespace DualDrill.Graphics;

public sealed partial record class GPUCommandBuffer<TBackend>(GPUHandle<TBackend, GPUCommandBuffer<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
