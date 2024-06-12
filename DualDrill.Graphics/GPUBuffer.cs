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
        var data = (TaskCompletionSource)gcHandle.Target;
        gcHandle.Free();
        if (status == WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_Success)
        {
            data.SetResult();
        }
        else
        {
            data.SetException(new GraphicsApiException($"Failed to map buffer, status {Enum.GetName(status)}"));
        }
    }

    unsafe void MapBufferAsyncImpl(GCHandle tcsHandle, GPUMapMode mode, int offset, int size)
    {
        WGPU.wgpuBufferMapAsync(Handle, (uint)mode, (uint)offset, (uint)size, &BufferMapped, (void*)GCHandle.ToIntPtr(tcsHandle));
    }

    public async ValueTask MapAsync(GPUMapMode mode, int offset, int size)
    {
        var data = new TaskCompletionSource();
        using var handle = new GCHandleDisposeWrapper(GCHandle.Alloc(data));
        MapBufferAsyncImpl(handle.Handle, mode, offset, size);
        await data.Task;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void BufferMappedLog(WGPUBufferMapAsyncStatus status, void* ptr)
    {
        Console.WriteLine($"Buffer Mapped {status}");
    }


    public unsafe void MapAsync2(GPUMapMode mode, int offset, int size)
    {
        WGPU.wgpuBufferMapAsync(Handle, (uint)mode, (uint)offset, (uint)size, &BufferMappedLog, null);
    }
    unsafe public Span<byte> GetConstMappedRange(int offset, int size)
    {
        return new Span<byte>(WGPU.wgpuBufferGetConstMappedRange(Handle, (nuint)offset, (nuint)size), size);
    }

    unsafe public void Unmap()
    {
        WGPU.wgpuBufferUnmap(Handle);
    }
}
