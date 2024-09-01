namespace DualDrill.Graphics;

public sealed partial record class GPUCommandEncoder<TBackend>(GPUHandle<TBackend, GPUCommandEncoder<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
