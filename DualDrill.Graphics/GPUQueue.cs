using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics;


public partial interface IGPUQueue : IDisposable
{
    public ValueTask OnSubmittedWorkDoneAsync(CancellationToken cancellation);
    public void Submit(IReadOnlyList<IGPUCommandBuffer> commandBuffers);
    public void WriteBuffer(IGPUBuffer buffer, ulong bufferOffset, ReadOnlySpan<byte> data);
    public void WriteTexture(GPUImageCopyTexture destination, ReadOnlySpan<byte> data, GPUImageDataLayout dataLayout, GPUExtent3D size);
}

public static partial class GraphicsExtension
{
    public static void WriteBuffer<T>(this IGPUQueue queue, IGPUBuffer buffer, ulong bufferOffset, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        queue.WriteBuffer(buffer, bufferOffset, MemoryMarshal.Cast<T, byte>(data));
    }
}

public sealed partial record class GPUQueue<TBackend>(GPUHandle<TBackend, GPUQueue<TBackend>> Handle)
: IDisposable, IGPUQueue
where TBackend : IBackend<TBackend>
{

    public ValueTask OnSubmittedWorkDoneAsync(CancellationToken cancellation)
    {
        return TBackend.Instance.OnSubmittedWorkDoneAsync(this, cancellation);
    }

    public void Submit(IReadOnlyList<IGPUCommandBuffer> commandBuffers)
    {
        TBackend.Instance.Submit(this, [.. commandBuffers.OfType<GPUCommandBuffer<TBackend>>()]);
    }

    unsafe public void WriteBuffer(IGPUBuffer buffer, ulong bufferOffset, ReadOnlySpan<byte> data)
    {
        var ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        TBackend.Instance.WriteBuffer(this, (GPUBuffer<TBackend>)buffer, bufferOffset, (nint)ptr, 0, (ulong)data.Length);
    }


    unsafe public void WriteTexture(GPUImageCopyTexture destination, ReadOnlySpan<byte> data, GPUImageDataLayout dataLayout, GPUExtent3D size)
    {
        TBackend.Instance.WriteTexture(this, destination, data, dataLayout, size);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}

