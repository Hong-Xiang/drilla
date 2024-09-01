namespace DualDrill.Graphics;

public sealed partial record class GPURenderPassEncoder<TBackend>(GPUHandle<TBackend, GPURenderPassEncoder<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
