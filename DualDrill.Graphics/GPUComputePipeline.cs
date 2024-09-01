namespace DualDrill.Graphics;

public sealed partial record class GPUComputePipeline<TBackend>(GPUHandle<TBackend, GPUComputePipeline<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
