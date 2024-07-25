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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    unsafe static void BufferMapped(WGPUBufferMapAsyncStatus status, void* ptr)
    {
        var handle = GCHandle.FromIntPtr((nint)ptr);
        var target = (TaskCompletionSource?)handle.Target ?? throw new GraphicsApiException($"GCHandle({(nint)ptr:X}) failed to recover target, target {handle.Target}");
        if (status == WGPUBufferMapAsyncStatus.WGPUBufferMapAsyncStatus_Success)
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
            WGPU.wgpuBufferMapAsync(Handle, (uint)mode, (uint)offset, (uint)size, &BufferMapped, (void*)GCHandle.ToIntPtr(tcsHandle));
        }
        goUnsafe(handle.Handle, mode, offset, size);
        await data.Task.ConfigureAwait(false);
        return new BufferMapping(this);
    }

    unsafe public ReadOnlySpan<byte> GetConstMappedRange(int offset, int size)
    {
        return new ReadOnlySpan<byte>(WGPU.wgpuBufferGetConstMappedRange(Handle, (nuint)offset, (nuint)size), size);
    }
    unsafe public Span<byte> GetMappedRange(int offset, int size)
    {
        return new Span<byte>(WGPU.wgpuBufferGetMappedRange(Handle, (nuint)offset, (nuint)size), size);
    }
    unsafe public void Unmap()
    {
        WGPU.wgpuBufferUnmap(Handle);
    }
}
