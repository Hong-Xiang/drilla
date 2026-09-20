namespace DualDrill.Graphics;

public readonly record struct GPUHandle<TBackend, TResource>
    where TBackend : IBackend<TBackend>
{
    private readonly Ownership? ownership;

    public GPUHandle(nint Pointer, object? Data = null)
    {
        this.Pointer = Pointer;
        this.Data = Data;
        ownership = Pointer == 0 ? null : new();
    }

    public nint Pointer { get; }

    public object? Data { get; }

    public void Deconstruct(out nint Pointer, out object? Data)
    {
        Pointer = this.Pointer;
        Data = this.Data;
    }

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
