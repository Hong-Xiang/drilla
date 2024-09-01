namespace DualDrill.Graphics;

public sealed partial record class GPURenderBundleEncoder<TBackend>(GPUHandle<TBackend, GPURenderBundleEncoder<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
