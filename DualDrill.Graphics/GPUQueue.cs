using DualDrill.Graphics.Interop;
using Silk.NET.Vulkan;
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
        TBackend.Instance.Submit(this, (IReadOnlyList<GPUCommandBuffer<TBackend>>)commandBuffers);
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


public sealed partial class GPUQueue
{
    public unsafe void Submit(ReadOnlySpan<GPUCommandBuffer> buffers)
    {
        var native = stackalloc WGPUCommandBufferImpl*[buffers.Length];
        for (var i = 0; i < buffers.Length; i++)
        {
            native[i] = buffers[i].Handle;
        }
        WGPU.QueueSubmit(Handle, (uint)buffers.Length, native);
    }

    public unsafe void WriteBuffer<T>(GPUBuffer buffer, ulong bufferOffset, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var byteSpan = MemoryMarshal.Cast<T, byte>(data);
        if (byteSpan.Length % 4 != 0)
        {
            throw new InvalidOperationException($"WriteBuffer data length must be a multiple of 4 bytes, got {byteSpan.Length}");
        }
        var ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(byteSpan));
        WGPU.QueueWriteBuffer(Handle, buffer.Handle, bufferOffset, ptr, (nuint)byteSpan.Length);
    }

    //public unsafe void WriteTexture(
    //    GPUImageCopyTexture destination,
    //    ReadOnlySpan<byte> data,
    //    GPUImageDataLayout layout,
    //    GPUExtent3D extent)
    //{
    //    var nativeDestination = new WGPUImageCopyTexture
    //    {
    //        aspect = destination.Aspect,
    //        mipLevel = (uint)destination.MipLevel,
    //        origin = new()
    //        {
    //            x = (uint)destination.Origin.X,
    //            y = (uint)destination.Origin.Y,
    //            z = (uint)destination.Origin.Z,
    //        },
    //        texture = destination.Texture.Handle,
    //    };
    //    var nativeLayout = new WGPUTextureDataLayout
    //    {
    //        offset = layout.Offset,
    //        bytesPerRow = (uint)layout.BytesPerRow,
    //        rowsPerImage = (uint)layout.RowsPerImage,
    //    };
    //    var nativeExtend = new WGPUExtent3D
    //    {
    //        width = (uint)extent.Width,
    //        height = (uint)extent.Height,
    //        depthOrArrayLayers = (uint)extent.DepthOrArrayLayers
    //    };
    //    var ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
    //    WGPU.QueueWriteTexture(
    //        Handle,
    //        &nativeDestination,
    //        ptr,
    //        (nuint)data.Length,
    //        &nativeLayout,
    //        &nativeExtend
    //    );
    //}

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void QueueWorkDone(GPUQueueWorkDoneStatus status, void* data)
    {
        var handle = GCHandle.FromIntPtr((nint)data);
        //var target = (TaskCompletionSource<WGPUQueueWorkDoneStatus>)handle.Target;
        //target.SetResult(status);
        var action = (Action?)handle.Target;
        if (action is not null)
        {
            action();
        }
        handle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void QueueWorkDoneAsync(GPUQueueWorkDoneStatus status, void* data)
    {
        var handle = GCHandle.FromIntPtr((nint)data);
        var target = handle.Target as TaskCompletionSource<GPUQueueWorkDoneStatus> ?? throw new GraphicsApiException($"GCHandle({(nint)data:X}) failed to recover target");
        target.SetResult(status);
        handle.Free();
    }

    public unsafe Task WaitSubmittedWorkDoneAsync(CancellationToken cancellation = default)
    {
        var tcs = new TaskCompletionSource<GPUQueueWorkDoneStatus>(cancellation);
        var handle = GCHandle.ToIntPtr(GCHandle.Alloc(tcs));
        WGPU.QueueOnSubmittedWorkDone(Handle, &QueueWorkDoneAsync, (void*)handle);
        return tcs.Task;
    }
    public unsafe void OnSubmittedWorkDone(Action next)
    {
        //var tcs = new TaskCompletionSource<WGPUQueueWorkDoneStatus>();
        //var handle = GCHandle.ToIntPtr(GCHandle.Alloc(tcs));
        var handle = GCHandle.ToIntPtr(GCHandle.Alloc(next));
        WGPU.QueueOnSubmittedWorkDone(Handle, &QueueWorkDone, (void*)handle);
        //return tcs.Task;
    }
}
