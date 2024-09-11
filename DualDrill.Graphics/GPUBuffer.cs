namespace DualDrill.Graphics;

public partial interface IGPUBuffer : IDisposable
{
    public ulong Length { get; }
    Span<byte> GetMappedRange(ulong offset, ulong size);
    ValueTask MapAsync(GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation);
    void Unmap();
}

public sealed partial record class GPUBuffer<TBackend>(GPUHandle<TBackend, GPUBuffer<TBackend>> Handle)
    : IDisposable, IGPUBuffer
    where TBackend : IBackend<TBackend>
{

    public required ulong Length { get; init; }
    public Span<byte> GetMappedRange(ulong offset, ulong size)
    {
        return TBackend.Instance.GetMappedRange(this, offset, size);
    }

    public async ValueTask MapAsync(
     GPUMapMode mode
    , ulong offset
    , ulong size
    , CancellationToken cancellation
    )
    {
        await TBackend.Instance.MapAsync(this, mode, offset, size, cancellation);
    }

    public void Unmap()
    {
        TBackend.Instance.Unmap(this);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
