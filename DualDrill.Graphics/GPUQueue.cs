using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUQueue
{
    public unsafe void Submit(ReadOnlySpan<GPUCommandBuffer> buffers)
    {
        var native = stackalloc WGPUCommandBufferImpl*[buffers.Length];
        for (var i = 0; i < buffers.Length; i++)
        {
            native[i] = buffers[i].Handle;
        }
        WGPU.wgpuQueueSubmit(Handle, (uint)buffers.Length, native);
    }

    public unsafe void WriteBuffer(GPUBuffer buffer, ulong bufferOffset, ReadOnlySpan<byte> data)
    {
        var ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        WGPU.wgpuQueueWriteBuffer(Handle, buffer.Handle, bufferOffset, ptr, (nuint)data.Length);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void QueueWorkDone(WGPUQueueWorkDoneStatus status, void* data)
    {
        var handle = GCHandle.FromIntPtr((nint)data);
        //var target = (TaskCompletionSource<WGPUQueueWorkDoneStatus>)handle.Target;
        //target.SetResult(status);
        var action = (Action)handle.Target;
        action();
        handle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void QueueWorkDoneAsync(WGPUQueueWorkDoneStatus status, void* data)
    {
        var handle = GCHandle.FromIntPtr((nint)data);
        var target = (TaskCompletionSource<WGPUQueueWorkDoneStatus>)handle.Target;
        if (target is null)
        {
            throw new GraphicsApiException($"GCHandle({(nint)data:X}) failed to recover target");
        }
        target.SetResult(status);
        handle.Free();
    }

    public unsafe Task WaitSubmittedWorkDoneAsync(CancellationToken cancellation = default)
    {
        var tcs = new TaskCompletionSource<WGPUQueueWorkDoneStatus>(cancellation);
        var handle = GCHandle.ToIntPtr(GCHandle.Alloc(tcs));
        WGPU.wgpuQueueOnSubmittedWorkDone(Handle, &QueueWorkDoneAsync, (void*)handle);
        return tcs.Task;
    }



    public unsafe void OnSubmittedWorkDone(Action next)
    {
        //var tcs = new TaskCompletionSource<WGPUQueueWorkDoneStatus>();
        //var handle = GCHandle.ToIntPtr(GCHandle.Alloc(tcs));
        var handle = GCHandle.ToIntPtr(GCHandle.Alloc(next));
        WGPU.wgpuQueueOnSubmittedWorkDone(Handle, &QueueWorkDone, (void*)handle);
        //return tcs.Task;
    }
}
