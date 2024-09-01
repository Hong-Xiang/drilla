namespace DualDrill.Graphics;

public sealed partial record class GPUQueue<TBackend>(GPUHandle<TBackend, GPUQueue<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
