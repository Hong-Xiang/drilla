namespace DualDrill.Graphics;

public readonly record struct GPUHandle<TBackend, TResource>(nint Pointer, object? Data = null)
    where TBackend : IBackend<TBackend>
{
    private readonly Ownership? ownership = Pointer == 0 ? null : new();

    public void Release(Action<GPUHandle<TBackend, TResource>> release)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (ownership?.TryRelease() is true)
        {
            release(this);
        }
    }

    private sealed class Ownership
    {
        private int released;

        public bool TryRelease()
        {
            return Interlocked.Exchange(ref released, 1) == 0;
        }
    }
}

public interface IGPUHandle : IDisposable
{
}

public interface IGPUNativeHandle<TBackend, TResource>
    where TBackend : IBackend<TBackend>
{
}
