namespace DualDrill.Graphics;

public sealed partial record class GPUBindGroup<TBackend>(GPUHandle<TBackend, GPUBindGroup<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
