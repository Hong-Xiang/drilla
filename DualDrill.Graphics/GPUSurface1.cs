namespace DualDrill.Graphics;

public sealed partial record class GPUSurface<TBackend>(GPUHandle<TBackend, GPUSurface<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
