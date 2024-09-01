namespace DualDrill.Graphics;

public sealed partial record class GPUShaderModule<TBackend>(GPUHandle<TBackend, GPUShaderModule<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{
    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
