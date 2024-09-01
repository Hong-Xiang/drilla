namespace DualDrill.Graphics;

public interface IGPUHandleDisposer<TBackend, TResource>
    where TBackend : IBackend<TBackend>
{
    internal void DisposeHandle(GPUHandle<TBackend, TResource> handle);
}

