namespace DualDrill.Graphics;

public sealed partial record class GPURenderBundle<TBackend>(GPUHandle<TBackend, GPURenderBundle<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
