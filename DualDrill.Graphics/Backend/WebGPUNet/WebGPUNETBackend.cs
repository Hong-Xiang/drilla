using Evergine.Bindings.WebGPU;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
namespace DualDrill.Graphics.Backend;
using static Evergine.Bindings.WebGPU.WebGPUNative;
using Backend = DualDrill.Graphics.Backend.WebGPUNETBackend;
using Native = Evergine.Bindings.WebGPU;

internal readonly record struct WebGPUNETHandle<THandle, TResource>(
    THandle Handle
) : IGPUHandle<Backend, TResource>
{
}

public sealed partial class WebGPUNETBackend : IBackend<Backend>
{
    public static Backend Instance { get; } = new();

    public unsafe static GPUInstance<Backend> CreateGPUInstance()
    {
        Native.WGPUInstanceDescriptor descriptor = new();
        var nativeInstance = wgpuCreateInstance(&descriptor);
        return new GPUInstance<Backend>(new(nativeInstance.Handle));
    }

    sealed record class AdapterData
    {
    }

    unsafe ValueTask<GPUAdapter<Backend>?> IBackend<Backend>.RequestAdapterAsync(GPUInstance<Backend> instance, GPURequestAdapterOptions options, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<GPUAdapter<Backend>?>(cancellationToken);
        // TODO: static method implementation (passing tcs using user GCHandle/data pointer) for better performance
        Native.WGPURequestAdapterOptions options_ = new();
        unsafe void OnAdapterRequestEnded(WGPURequestAdapterStatus status, WGPUAdapter candidateAdapter, char* message, void* pUserData)
        {
            if (status == WGPURequestAdapterStatus.Success)
            {
                tcs.SetResult(new(new(candidateAdapter.Handle, new AdapterData())));

                // TODO: update AdapterProperties and AdapterLimits

                //WGPUAdapterProperties properties;
                //wgpuAdapterGetProperties(candidateAdapter, &properties);

                //WGPUSupportedLimits limits;
                //wgpuAdapterGetLimits(candidateAdapter, &limits);

                //AdapterProperties = properties;
                //AdapterLimits = limits;
            }
            else
            {
                tcs.SetException(new GraphicsApiException<Backend>($"Could not get WebGPU adapter: {Marshal.PtrToStringUTF8((nint)message)}"));
            }
        }
        wgpuInstanceRequestAdapter(instance.Handle.ToNative(),
                                        &options_,
                                        OnAdapterRequestEnded, null);
        return new(tcs.Task);
    }

    unsafe ValueTask<GPUDevice<Backend>> IBackend<Backend>.RequestDeviceAsync(GPUAdapter<Backend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        WGPUDeviceDescriptor descriptor_ = new();
        // TODO: filling descriptor fields

        var tcs = new TaskCompletionSource<GPUDevice<Backend>>();
        void OnDeviceRequestEnded(WGPURequestDeviceStatus status, WGPUDevice device, char* message, void* pUserData)
        {
            if (status == WGPURequestDeviceStatus.Success)
            {
                tcs.SetResult(new(new(device.Handle)));
            }
            else
            {
                tcs.SetException(new GraphicsApiException<Backend>($"Could not get WebGPU device: {Marshal.PtrToStringUTF8((nint)message)}"));
            }
        }
        wgpuAdapterRequestDevice(adapter.Handle.ToNative(), &descriptor_, OnDeviceRequestEnded, null);
        return new(tcs.Task);
    }

    unsafe GPUBuffer<Backend> IBackend<Backend>.CreateBuffer(GPUDevice<Backend> device, GPUBufferDescriptor descriptor)
    {
        var alignedSize = (descriptor.Size + 3UL) & ~3UL;
        //Debug.Assert(descriptor.Size == alignedSize, "Buffer byte size should be multiple of 4");
        WGPUBufferDescriptor nativeDescriptor = new()
        {
            mappedAtCreation = descriptor.MappedAtCreation.Value,
            size = alignedSize,
            usage = ToNative(descriptor.Usage),
        };
        var handle = wgpuDeviceCreateBuffer(device.Handle.ToNative(), &nativeDescriptor);
        return new(new(handle.Handle))
        {
            Length = alignedSize
        };
    }

    private WGPUBufferUsage ToNative(GPUBufferUsage value)
    {
        return (WGPUBufferUsage)value;
    }

    GPUTextureView<Backend> IBackend<Backend>.CreateTextureView(GPUTexture<Backend> texture, GPUTextureViewDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUDevice> IBackend<Backend>.RequestDeviceAsyncLegacy(GPUAdapter<Backend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }

    GPUTextureFormat IBackend<Backend>.GetPreferredCanvasFormat(GPUInstance<Backend> handle)
    {
        // TODO: consider how to implement this properly
        return GPUTextureFormat.BGRA8Unorm;
    }

    GPUTexture<Backend> IBackend<Backend>.CreateTexture(GPUDevice<Backend> handle, GPUTextureDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUSampler<Backend> IBackend<Backend>.CreateSampler(GPUDevice<Backend> handle, GPUSamplerDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroupLayout<Backend> IBackend<Backend>.CreateBindGroupLayout(GPUDevice<Backend> handle, GPUBindGroupLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUPipelineLayout<Backend> IBackend<Backend>.CreatePipelineLayout(GPUDevice<Backend> handle, GPUPipelineLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroup<Backend> IBackend<Backend>.CreateBindGroup(GPUDevice<Backend> handle, GPUBindGroupDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUShaderModule<Backend> IBackend<Backend>.CreateShaderModule(GPUDevice<Backend> handle, GPUShaderModuleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePipeline<Backend> IBackend<Backend>.CreateComputePipeline(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderPipeline<Backend> IBackend<Backend>.CreateRenderPipeline(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUComputePipeline<Backend>> IBackend<Backend>.CreateComputePipelineAsyncAsync(GPUDevice<Backend> handle, GPUComputePipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPURenderPipeline<Backend>> IBackend<Backend>.CreateRenderPipelineAsyncAsync(GPUDevice<Backend> handle, GPURenderPipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    GPUCommandEncoder<Backend> IBackend<Backend>.CreateCommandEncoder(GPUDevice<Backend> handle, GPUCommandEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderBundleEncoder<Backend> IBackend<Backend>.CreateRenderBundleEncoder(GPUDevice<Backend> handle, GPURenderBundleEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUQuerySet<Backend> IBackend<Backend>.CreateQuerySet(GPUDevice<Backend> handle, GPUQuerySetDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask IBackend<Backend>.MapAsyncAsync(GPUBuffer<Backend> handle, GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    ReadOnlySpan<byte> IBackend<Backend>.GetMappedRange(GPUBuffer<Backend> handle, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.Unmap(GPUBuffer<Backend> handle)
    {
        throw new NotImplementedException();
    }

    GPURenderPassEncoder<Backend> IBackend<Backend>.BeginRenderPass(GPUCommandEncoder<Backend> handle, GPURenderPassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePassEncoder<Backend> IBackend<Backend>.BeginComputePass(GPUCommandEncoder<Backend> handle, GPUComputePassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.CopyBufferToBuffer(GPUCommandEncoder<Backend> handle, GPUBuffer<Backend> source, ulong sourceOffset, GPUBuffer<Backend> destination, ulong destinationOffset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.CopyBufferToTexture(GPUCommandEncoder<Backend> handle, GPUImageCopyBuffer source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.CopyTextureToBuffer(GPUCommandEncoder<Backend> handle, GPUImageCopyTexture source, GPUImageCopyBuffer destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.CopyTextureToTexture(GPUCommandEncoder<Backend> handle, GPUImageCopyTexture source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.ClearBuffer(GPUCommandEncoder<Backend> handle, GPUBuffer<Backend> buffer, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.ResolveQuerySet(GPUCommandEncoder<Backend> handle, GPUQuerySet<Backend> querySet, uint firstQuery, uint queryCount, GPUBuffer<Backend> destination, ulong destinationOffset)
    {
        throw new NotImplementedException();
    }

    GPUCommandBuffer<Backend> IBackend<Backend>.Finish(GPUCommandEncoder<Backend> handle, GPUCommandBufferDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.PushDebugGroup(GPUCommandEncoder<Backend> handle, string groupLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.PopDebugGroup(GPUCommandEncoder<Backend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.InsertDebugMarker(GPUCommandEncoder<Backend> handle, string markerLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.PushDebugGroup(GPUComputePassEncoder<Backend> handle, string groupLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.PopDebugGroup(GPUComputePassEncoder<Backend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.InsertDebugMarker(GPUComputePassEncoder<Backend> handle, string markerLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetPipeline(GPUComputePassEncoder<Backend> handle, GPUComputePipeline<Backend> pipeline)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.DispatchWorkgroups(GPUComputePassEncoder<Backend> handle, uint workgroupCountX, uint workgroupCountY, uint workgroupCountZ)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.DispatchWorkgroupsIndirect(GPUComputePassEncoder<Backend> handle, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.End(GPUComputePassEncoder<Backend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetBindGroup(GPUComputePassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    void IBackend<Backend>.SetBindGroup(GPUComputePassEncoder<Backend> handle, int index, GPUBindGroup<Backend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }
}

public static partial class WebGPUNetBackendExtensions
{
    //public static WGPUInstance ToNative(this GPUHandle<Backend, GPUInstance<Backend>> instance)
    //    => new(instance.Pointer);
    //public static WGPUAdapter ToNative(this GPUHandle<Backend, GPUAdapter<Backend>> adapter)
    //    => new(adapter.Pointer);
    //public static WGPUDevice ToNative(this GPUHandle<Backend, GPUDevice<Backend>> device) 
    //    => new(device.Pointer);
}
