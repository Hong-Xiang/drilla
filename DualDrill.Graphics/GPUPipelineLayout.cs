namespace DualDrill.Graphics;

public sealed partial record class GPUPipelineLayout<TBackend>(GPUHandle<TBackend, GPUPipelineLayout<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
