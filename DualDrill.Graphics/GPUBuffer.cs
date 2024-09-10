using DualDrill.Graphics.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics;


public partial interface IGPUBuffer : IDisposable
{
    public ulong Length { get; }
    ReadOnlySpan<byte> GetMappedRange(ulong offset, ulong size);
    ValueTask MapAsyncAsync(GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation);
    void Unmap();
}

public sealed partial record class GPUBuffer<TBackend>(GPUHandle<TBackend, GPUBuffer<TBackend>> Handle)
    : IDisposable, IGPUBuffer
    where TBackend : IBackend<TBackend>
{

    public required ulong Length { get; init; }
    public ReadOnlySpan<byte> GetMappedRange(ulong offset, ulong size)
    {
        return TBackend.Instance.GetMappedRange(this, offset, size);
    }

    public ValueTask MapAsyncAsync(
     GPUMapMode mode
    , ulong offset
    , ulong size
    , CancellationToken cancellation
    )
    {
        return TBackend.Instance.MapAsyncAsync(this, mode, offset, size, cancellation);
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


public sealed partial class GPUBuffer
{

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void BufferMapped(GPUBufferMapAsyncStatus status, void* ptr)
    {
        var handle = GCHandle.FromIntPtr((nint)ptr);
        var target = (TaskCompletionSource?)handle.Target ?? throw new GraphicsApiException($"GCHandle({(nint)ptr:X}) failed to recover target, target {handle.Target}");
        if (status == GPUBufferMapAsyncStatus.Success)
        {
            target.SetResult();
        }
        else
        {
            target.SetException(new GraphicsApiException($"Failed to map buffer, status {Enum.GetName(status)}"));
        }
    }

    public readonly record struct BufferMapping(GPUBuffer Buffer) : IDisposable
    {
        public void Dispose()
        {
            Buffer.Unmap();
        }
    }

    public async ValueTask<BufferMapping> MapAsync(GPUMapMode mode, int offset, int size, CancellationToken cancellation = default)
    {
        var data = new TaskCompletionSource(cancellation);
        using var handle = new DisposableGCHandle(GCHandle.Alloc(data));
        unsafe void goUnsafe(GCHandle tcsHandle, GPUMapMode mode, int offset, int size)
        {
            WGPU.BufferMapAsync(Handle, (uint)mode, (uint)offset, (uint)size, &BufferMapped, (void*)GCHandle.ToIntPtr(tcsHandle));
        }
        goUnsafe(handle.Handle, mode, offset, size);
        await data.Task.ConfigureAwait(false);
        return new BufferMapping(this);
    }

    unsafe public ReadOnlySpan<byte> GetConstMappedRange(int offset, int size)
    {
        return new ReadOnlySpan<byte>(WGPU.BufferGetConstMappedRange(Handle, (nuint)offset, (nuint)size), size);
    }
    unsafe public Span<byte> GetMappedRange(int offset, int size)
    {
        return new Span<byte>(WGPU.BufferGetMappedRange(Handle, (nuint)offset, (nuint)size), size);
    }
    unsafe public void Unmap()
    {
        WGPU.BufferUnmap(Handle);
    }
}
