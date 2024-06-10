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


    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void QueueWorkDone(WGPUQueueWorkDoneStatus status, void* data)
    {
        var handle = GCHandle.FromIntPtr((nint)data);
        var target = (TaskCompletionSource<WGPUQueueWorkDoneStatus>)handle.Target;
        target.SetResult(status);
        handle.Free();
    }


    public unsafe Task OnSubmittedWorkDone()
    {
        var tcs = new TaskCompletionSource<WGPUQueueWorkDoneStatus>();
        var handle = GCHandle.ToIntPtr(GCHandle.Alloc(tcs));
        WGPU.wgpuQueueOnSubmittedWorkDone(Handle, &QueueWorkDone, (void*)handle);
        return tcs.Task;
    }
}
