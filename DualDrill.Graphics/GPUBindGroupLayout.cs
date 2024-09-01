namespace DualDrill.Graphics;

public sealed partial record class GPUBindGroupLayout<TBackend>(GPUHandle<TBackend, GPUBindGroupLayout<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
