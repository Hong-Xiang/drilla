namespace DualDrill.Graphics;

public sealed partial record class GPUComputePassEncoder<TBackend>(GPUHandle<TBackend, GPUComputePassEncoder<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
