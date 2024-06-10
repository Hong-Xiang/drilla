using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics;

public sealed partial class GPUBuffer
{
    public unsafe static GPUBuffer Create(GPUDevice device, in GPUBufferDescriptor descriptor)
    {
        WGPUBufferDescriptor nativeDescriptor = new()
        {
            mappedAtCreation = descriptor.MappedAtCreation ? 1u : 0u,
            size = (ulong)descriptor.Size,
            usage = (uint)descriptor.Usage,
        };
        var handle = WGPU.wgpuDeviceCreateBuffer(device.NativePointer, &nativeDescriptor);
        return new(handle);
    }

    unsafe record struct BufferMapData(
        NativeHandle<WGPUApiWrapper, WGPUBufferImpl> Handle,
        TaskCompletionSource<Memory<byte>> TaskCompletionSource)
    {
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void BufferMapped(WGPUBufferMapAsyncStatus status, void* ptr)
    {
        var gcHandle = GCHandle.FromIntPtr((nint)ptr);
        var data = (TaskCompletionSource<WGPUBufferMapAsyncStatus>)gcHandle.Target;
        data.SetResult(status);
    }

    unsafe void MapBufferAsyncImpl(GCHandle tcsHandle, GPUMapMode mode, int offset, int size)
    {
        WGPU.wgpuBufferMapAsync(Handle, (uint)mode, (uint)offset, (uint)size, &BufferMapped, (void*)GCHandle.ToIntPtr(tcsHandle));
    }

    public async ValueTask MapAsync(GPUMapMode mode, int offset, int size)
    {
        var data = new TaskCompletionSource<WGPUBufferMapAsyncStatus>();
        using var handle = new GCHandleDisposeWrapper(GCHandle.Alloc(data));
        MapBufferAsyncImpl(handle.Handle, mode, offset, size);
        var status = await data.Task;
        if (status != WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_Success)
        {
            throw new GraphicsApiException($"Failed to map buffer, status {Enum.GetName(status)}");
        }
    }
}
