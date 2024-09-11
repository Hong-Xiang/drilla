namespace DualDrill.Graphics;

public readonly record struct GPUHandle<TBackend, TResource>(nint Pointer, object? Data = null)
    where TBackend : IBackend<TBackend>
{
}

public interface IGPUHandle : IDisposable
{
}

public interface IGPUNativeHandle<TBackend, TResource>
    where TBackend : IBackend<TBackend>
{
}
