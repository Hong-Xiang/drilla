namespace DualDrill.Graphics;

public sealed partial record class GPUQuerySet<TBackend>(GPUHandle<TBackend, GPUQuerySet<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
