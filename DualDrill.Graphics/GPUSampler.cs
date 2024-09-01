namespace DualDrill.Graphics;

public sealed partial record class GPUSampler<TBackend>(GPUHandle<TBackend, GPUSampler<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
