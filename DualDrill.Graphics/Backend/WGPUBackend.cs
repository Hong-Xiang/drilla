using DualDrill.Graphics.Interop;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics.Backend;

public static class WebGPUApiExtension
{
    public static GPUShaderModule CreateShaderModule(this GPUDevice device, string code) => GPUShaderModule.Create(device, code);
}

public sealed class WGPUBackend : IBackend<WGPUBackend>
{
    public static WGPUBackend Instance { get; } = new();

    unsafe public GPUInstance<WGPUBackend> CreateGPUInstance()
    {
        WGPUInstanceDescriptor descriptor = new();
        var pointer = WGPU.CreateInstance(&descriptor);
        return new(new((nint)pointer));
    }
    public static unsafe GPUSurface CreateSurface(Silk.NET.Windowing.IView view, GPUInstance<WGPUBackend> instance)
    {
        WGPUSurfaceDescriptor descriptor = new();
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND = new();
        surfaceDescriptorFromWindowsHWND.chain = new WGPUChainedStruct
        {
            next = null,
            sType = WGPUSType.SurfaceDescriptorFromWindowsHWND
        };
        surfaceDescriptorFromWindowsHWND.hwnd = (void*)(((nint, nint, nint)?)view.Native.Win32).Value.Item1;
        surfaceDescriptorFromWindowsHWND.hinstance = (void*)(((nint, nint, nint)?)view.Native.Win32).Value.Item3;
        WGPUSurfaceDescriptorFromWindowsHWND surfaceDescriptorFromWindowsHWND2 = surfaceDescriptorFromWindowsHWND;
        descriptor.nextInChain = (WGPUChainedStruct*)&surfaceDescriptorFromWindowsHWND;
        WGPUSurfaceImpl* result = WGPU.InstanceCreateSurface(ToNativePointer(instance.Handle), &descriptor);
        if (result is null)
        {
            throw new GraphicsApiException("Failed to create surface");
        }
        return new(result);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestAdaptorCallback(
        GPURequestAdapterStatus status,
        WGPUAdapterImpl* adapter,
        sbyte* message,
        void* data)
    {
        RequestCallback<WGPUNativeApiInterop, WGPUAdapterImpl, GPURequestAdapterStatus>.Callback(status, adapter, message, data);
    }

    unsafe ValueTask<GPUAdapter<WGPUBackend>?> IBackend<WGPUBackend>.RequestAdapterAsync(
          GPUInstance<WGPUBackend> instance,
          GPURequestAdapterOptions options,
          CancellationToken cancellation)
    {
        var options_ = new WGPURequestAdapterOptions
        {
            powerPreference = options.PowerPreference,
            forceFallbackAdapter = (GPUBool)options.ForceFallbackAdapter
        };
        //if (options.CompatibleSurface is GPUSurface surface)
        //{
        //    options_.compatibleSurface = ToNativePointer(surface.Handle);
        //}
        var result = new RequestCallbackResult<WGPUAdapterImpl, GPURequestAdapterStatus>();
        WGPU.InstanceRequestAdapter(
            ToNativePointer(instance.Handle),
            &options_,
            &RequestAdaptorCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUAdapter<WGPUBackend>)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        return ValueTask.FromResult(new GPUAdapter<WGPUBackend>(new((nint)result.Handle)));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestDeviceCallback(GPURequestDeviceStatus status, WGPUDeviceImpl* device, sbyte* message, void* data)
    {
        RequestCallback<WGPUNativeApiInterop, WGPUDeviceImpl, GPURequestDeviceStatus>.Callback(status, device, message, data);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void DeviceUncapturedErrorCallback(GPUErrorType errorType, sbyte* message, void* data)
    {
        Console.Error.WriteLine($"Device uncaptured error type = {Enum.GetName(errorType)}, message {Marshal.PtrToStringUTF8((nint)message)}");
    }


    unsafe ValueTask<GPUDevice> IBackend<WGPUBackend>.RequestDeviceAsyncLegacy(
        GPUAdapter<WGPUBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    )
    {
        WGPUDeviceDescriptor descriptor_ = new();
        var result = new RequestCallbackResult<WGPUDeviceImpl, GPURequestDeviceStatus>();
        WGPU.AdapterRequestDevice(
            ToNativePointer(adapter.Handle),
            &descriptor_,
            &RequestDeviceCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUDevice)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        WGPU.DeviceSetUncapturedErrorCallback(result.Handle, &DeviceUncapturedErrorCallback, null);
        return ValueTask.FromResult(new GPUDevice(result.Handle));
    }

    void IGPUHandleDisposer<WGPUBackend, GPUAdapter<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUAdapter<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    private static unsafe WGPUInstanceImpl* ToNativePointer(GPUHandle<WGPUBackend, GPUInstance<WGPUBackend>> handle) => (WGPUInstanceImpl*)handle.Pointer;
    unsafe void IGPUHandleDisposer<WGPUBackend, GPUInstance<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUInstance<WGPUBackend>> handle)
    {
        WGPU.InstanceRelease(ToNativePointer(handle));
    }
    private static unsafe WGPUAdapterImpl* ToNativePointer(GPUHandle<WGPUBackend, GPUAdapter<WGPUBackend>> handle) => (WGPUAdapterImpl*)handle.Pointer;

    ValueTask<GPUDevice<WGPUBackend>> IBackend<WGPUBackend>.RequestDeviceAsync(GPUAdapter<WGPUBackend> adapter, GPUDeviceDescriptor descriptor, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    GPUBuffer<WGPUBackend> IBackend<WGPUBackend>.CreateBuffer(GPUDevice<WGPUBackend> device, GPUBufferDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUTextureView<WGPUBackend> IBackend<WGPUBackend>.CreateTextureView(GPUTexture<WGPUBackend> texture, GPUTextureViewDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUDevice<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUDevice<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUTexture<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUTexture<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUBuffer<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUBuffer<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUTextureView<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUTextureView<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUBindGroup<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUBindGroup<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUBindGroupLayout<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUBindGroupLayout<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUCommandBuffer<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUCommandBuffer<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUCommandEncoder<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUCommandEncoder<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUComputePassEncoder<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUComputePassEncoder<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUComputePipeline<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUComputePipeline<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUPipelineLayout<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUPipelineLayout<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUQuerySet<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUQuerySet<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUQueue<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUQueue<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPURenderBundle<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPURenderBundle<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPURenderBundleEncoder<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPURenderBundleEncoder<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPURenderPassEncoder<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPURenderPassEncoder<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPURenderPipeline<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPURenderPipeline<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUSampler<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUSampler<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUShaderModule<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUShaderModule<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    void IGPUHandleDisposer<WGPUBackend, GPUSurface<WGPUBackend>>.DisposeHandle(GPUHandle<WGPUBackend, GPUSurface<WGPUBackend>> handle)
    {
        throw new NotImplementedException();
    }

    GPUTextureFormat IBackend<WGPUBackend>.GetPreferredCanvasFormat(GPUInstance<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    GPUTexture<WGPUBackend> IBackend<WGPUBackend>.CreateTexture(GPUDevice<WGPUBackend> handle, GPUTextureDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUSampler<WGPUBackend> IBackend<WGPUBackend>.CreateSampler(GPUDevice<WGPUBackend> handle, GPUSamplerDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroupLayout<WGPUBackend> IBackend<WGPUBackend>.CreateBindGroupLayout(GPUDevice<WGPUBackend> handle, GPUBindGroupLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUPipelineLayout<WGPUBackend> IBackend<WGPUBackend>.CreatePipelineLayout(GPUDevice<WGPUBackend> handle, GPUPipelineLayoutDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUBindGroup<WGPUBackend> IBackend<WGPUBackend>.CreateBindGroup(GPUDevice<WGPUBackend> handle, GPUBindGroupDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUShaderModule<WGPUBackend> IBackend<WGPUBackend>.CreateShaderModule(GPUDevice<WGPUBackend> handle, GPUShaderModuleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePipeline<WGPUBackend> IBackend<WGPUBackend>.CreateComputePipeline(GPUDevice<WGPUBackend> handle, GPUComputePipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderPipeline<WGPUBackend> IBackend<WGPUBackend>.CreateRenderPipeline(GPUDevice<WGPUBackend> handle, GPURenderPipelineDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUComputePipeline<WGPUBackend>> IBackend<WGPUBackend>.CreateComputePipelineAsyncAsync(GPUDevice<WGPUBackend> handle, GPUComputePipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPURenderPipeline<WGPUBackend>> IBackend<WGPUBackend>.CreateRenderPipelineAsyncAsync(GPUDevice<WGPUBackend> handle, GPURenderPipelineDescriptor descriptor, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    GPUCommandEncoder<WGPUBackend> IBackend<WGPUBackend>.CreateCommandEncoder(GPUDevice<WGPUBackend> handle, GPUCommandEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPURenderBundleEncoder<WGPUBackend> IBackend<WGPUBackend>.CreateRenderBundleEncoder(GPUDevice<WGPUBackend> handle, GPURenderBundleEncoderDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUQuerySet<WGPUBackend> IBackend<WGPUBackend>.CreateQuerySet(GPUDevice<WGPUBackend> handle, GPUQuerySetDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    ValueTask IBackend<WGPUBackend>.MapAsyncAsync(GPUBuffer<WGPUBackend> handle, GPUMapMode mode, ulong offset, ulong size, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    ReadOnlySpan<byte> IBackend<WGPUBackend>.GetMappedRange(GPUBuffer<WGPUBackend> handle, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.Unmap(GPUBuffer<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    GPURenderPassEncoder<WGPUBackend> IBackend<WGPUBackend>.BeginRenderPass(GPUCommandEncoder<WGPUBackend> handle, GPURenderPassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    GPUComputePassEncoder<WGPUBackend> IBackend<WGPUBackend>.BeginComputePass(GPUCommandEncoder<WGPUBackend> handle, GPUComputePassDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.CopyBufferToBuffer(GPUCommandEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> source, ulong sourceOffset, GPUBuffer<WGPUBackend> destination, ulong destinationOffset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.CopyBufferToTexture(GPUCommandEncoder<WGPUBackend> handle, GPUImageCopyBuffer source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.CopyTextureToBuffer(GPUCommandEncoder<WGPUBackend> handle, GPUImageCopyTexture source, GPUImageCopyBuffer destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.CopyTextureToTexture(GPUCommandEncoder<WGPUBackend> handle, GPUImageCopyTexture source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.ClearBuffer(GPUCommandEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> buffer, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.ResolveQuerySet(GPUCommandEncoder<WGPUBackend> handle, GPUQuerySet<WGPUBackend> querySet, uint firstQuery, uint queryCount, GPUBuffer<WGPUBackend> destination, ulong destinationOffset)
    {
        throw new NotImplementedException();
    }

    GPUCommandBuffer<WGPUBackend> IBackend<WGPUBackend>.Finish(GPUCommandEncoder<WGPUBackend> handle, GPUCommandBufferDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PushDebugGroup(GPUCommandEncoder<WGPUBackend> handle, string groupLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PopDebugGroup(GPUCommandEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.InsertDebugMarker(GPUCommandEncoder<WGPUBackend> handle, string markerLabel)
    {
        throw new NotImplementedException();
    }
    void IBackend<WGPUBackend>.SetBindGroup(GPUComputePassEncoder<WGPUBackend> handle, int index, GPUBindGroup<WGPUBackend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PushDebugGroup(GPUComputePassEncoder<WGPUBackend> handle, string groupLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PopDebugGroup(GPUComputePassEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.InsertDebugMarker(GPUComputePassEncoder<WGPUBackend> handle, string markerLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetPipeline(GPUComputePassEncoder<WGPUBackend> handle, GPUComputePipeline<WGPUBackend> pipeline)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DispatchWorkgroups(GPUComputePassEncoder<WGPUBackend> handle, uint workgroupCountX, uint workgroupCountY, uint workgroupCountZ)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DispatchWorkgroupsIndirect(GPUComputePassEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> indirectBuffer, ulong indirectOffset)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.End(GPUComputePassEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetBindGroup(GPUComputePassEncoder<WGPUBackend> handle, int index, GPUBindGroup<WGPUBackend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    GPUBindGroupLayout<WGPUBackend> IBackend<WGPUBackend>.GetBindGroupLayout(GPUComputePipeline<WGPUBackend> handle, ulong index)
    {
        throw new NotImplementedException();
    }

    ValueTask IBackend<WGPUBackend>.OnSubmittedWorkDoneAsync(GPUQueue<WGPUBackend> handle, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.Submit(GPUQueue<WGPUBackend> handle, ReadOnlySpan<GPUCommandBuffer<WGPUBackend>> commandBuffers)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.WriteBuffer(GPUQueue<WGPUBackend> handle, GPUBuffer<WGPUBackend> buffer, ulong bufferOffset, nint data, ulong dataOffset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.WriteTexture(GPUQueue<WGPUBackend> handle, GPUImageCopyTexture destination, nint data, GPUTextureDataLayout dataLayout, GPUExtent3D size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.Draw(GPURenderBundleEncoder<WGPUBackend> handle, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DrawIndexed(GPURenderBundleEncoder<WGPUBackend> handle, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DrawIndexedIndirect(GPURenderBundleEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> indirectBuffer, ulong indirectOffset)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DrawIndirect(GPURenderBundleEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> indirectBuffer, ulong indirectOffset)
    {
        throw new NotImplementedException();
    }

    GPURenderBundle<WGPUBackend> IBackend<WGPUBackend>.Finish(GPURenderBundleEncoder<WGPUBackend> handle, GPURenderBundleDescriptor descriptor)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.InsertDebugMarker(GPURenderBundleEncoder<WGPUBackend> handle, string markerLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PopDebugGroup(GPURenderBundleEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PushDebugGroup(GPURenderBundleEncoder<WGPUBackend> handle, string groupLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetBindGroup(GPURenderBundleEncoder<WGPUBackend> handle, int index, GPUBindGroup<WGPUBackend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetBindGroup(GPURenderBundleEncoder<WGPUBackend> handle, int index, GPUBindGroup<WGPUBackend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetIndexBuffer(GPURenderBundleEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetPipeline(GPURenderBundleEncoder<WGPUBackend> handle, GPURenderPipeline<WGPUBackend> pipeline)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetVertexBuffer(GPURenderBundleEncoder<WGPUBackend> handle, int slot, GPUBuffer<WGPUBackend>? buffer, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.BeginOcclusionQuery(GPURenderPassEncoder<WGPUBackend> handle, uint queryIndex)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.Draw(GPURenderPassEncoder<WGPUBackend> handle, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DrawIndexed(GPURenderPassEncoder<WGPUBackend> handle, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DrawIndexedIndirect(GPURenderPassEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> indirectBuffer, ulong indirectOffset)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.DrawIndirect(GPURenderPassEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> indirectBuffer, ulong indirectOffset)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.End(GPURenderPassEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.EndOcclusionQuery(GPURenderPassEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.ExecuteBundles(GPURenderPassEncoder<WGPUBackend> handle, ReadOnlySpan<GPURenderBundle<WGPUBackend>> bundles)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.InsertDebugMarker(GPURenderPassEncoder<WGPUBackend> handle, string markerLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PopDebugGroup(GPURenderPassEncoder<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.PushDebugGroup(GPURenderPassEncoder<WGPUBackend> handle, string groupLabel)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetBindGroup(GPURenderPassEncoder<WGPUBackend> handle, int index, GPUBindGroup<WGPUBackend>? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetBindGroup(GPURenderPassEncoder<WGPUBackend> handle, int index, GPUBindGroup<WGPUBackend>? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetBlendConstant(GPURenderPassEncoder<WGPUBackend> handle, GPUColor color)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetIndexBuffer(GPURenderPassEncoder<WGPUBackend> handle, GPUBuffer<WGPUBackend> buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetPipeline(GPURenderPassEncoder<WGPUBackend> handle, GPURenderPipeline<WGPUBackend> pipeline)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetScissorRect(GPURenderPassEncoder<WGPUBackend> handle, uint x, uint y, uint width, uint height)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetStencilReference(GPURenderPassEncoder<WGPUBackend> handle, uint reference)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetVertexBuffer(GPURenderPassEncoder<WGPUBackend> handle, int slot, GPUBuffer<WGPUBackend>? buffer, ulong offset, ulong size)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.SetViewport(GPURenderPassEncoder<WGPUBackend> handle, float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        throw new NotImplementedException();
    }

    GPUBindGroupLayout<WGPUBackend> IBackend<WGPUBackend>.GetBindGroupLayout(GPURenderPipeline<WGPUBackend> handle, ulong index)
    {
        throw new NotImplementedException();
    }

    ValueTask<GPUCompilationInfo> IBackend<WGPUBackend>.GetCompilationInfoAsync(GPUShaderModule<WGPUBackend> handle, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.Configure(GPUSurface<WGPUBackend> handle, GPUSurfaceConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    GPUTexture<WGPUBackend> IBackend<WGPUBackend>.GetCurrentTexture(GPUSurface<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    void IBackend<WGPUBackend>.Unconfigure(GPUSurface<WGPUBackend> handle)
    {
        throw new NotImplementedException();
    }

    GPUTextureView<WGPUBackend> IBackend<WGPUBackend>.CreateView(GPUTexture<WGPUBackend> handle, GPUTextureViewDescriptor descriptor)
    {
        throw new NotImplementedException();
    }
}

//public unsafe sealed class WebGPUGraphicsApi : IGraphicsApi<WebGPUGraphicsApi, nint>, IDisposable
//{
//    internal readonly static Silk.NET.WebGPU.WebGPU Api = Silk.NET.WebGPU.WebGPU.GetApi();

//    public readonly static WebGPUGraphicsApi Instance = new();
//    internal WebGPUGraphicsApi() { }

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.CreateInstance()
//    {
//        var desc = new Silk.NET.WebGPU.InstanceDescriptor();
//        var result = Api.CreateInstance(&desc);
//        if (result is null)
//        {
//            throw new GraphicsApiException("Failed to create GPUInstance using WebGPU Graphics API");
//        }
//        return ValueTask.FromResult((nint)result);
//    }
//    ValueTask IGraphicsApi<WebGPUGraphicsApi, nint>.DestroyInstance(nint instance)
//    {
//        Api.InstanceRelease((Silk.NET.WebGPU.Instance*)instance);
//        return ValueTask.CompletedTask;
//    }
//    bool IGraphicsApi<WebGPUGraphicsApi, nint>.IsValidHandle(nint handle) => (void*)handle is not null;
//    ref struct RequestAdapterResult
//    {
//        public Silk.NET.WebGPU.Adapter* Adapter;
//        public string? Message;
//    }

//    readonly PfnRequestAdapterCallback RequestAdapterCallback = new((status, adapter, message, data) =>
//        {
//            var resultData = (RequestAdapterResult*)data;
//            if (status == RequestAdapterStatus.Success && resultData->Adapter is null)
//            {
//                resultData->Adapter = adapter;
//            }
//            else
//            {
//                resultData->Message = Marshal.PtrToStringUTF8((nint)message);
//            }
//        });

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.InstanceRequestAdapter(nint instance)
//    {
//        var result = new RequestAdapterResult();
//        var options = new RequestAdapterOptions()
//        {
//            PowerPreference = PowerPreference.HighPerformance
//        };
//        Api.InstanceRequestAdapter((Silk.NET.WebGPU.Instance*)instance, in options, RequestAdapterCallback, &result);
//        if (result.Adapter is null)
//        {
//            throw new GraphicsApiException($"Failed to create adapter, {result.Message}");
//        }
//        return ValueTask.FromResult((nint)result.Adapter);
//    }

//    ValueTask IGraphicsApi<WebGPUGraphicsApi, nint>.DestroyAdapter(nint instance)
//    {
//        return ValueTask.CompletedTask;
//    }

//    public void Dispose()
//    {
//        RequestAdapterCallback.Dispose();
//    }

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.AdapterRequestDevice(nint adapter)
//    {
//        throw new NotImplementedException();
//    }

//    ValueTask<nint> IGraphicsApi<WebGPUGraphicsApi, nint>.DeviceRequestQueue(nint device)
//    {
//        throw new NotImplementedException();
//    }
//}

//public unsafe static class WebGPUGraphicsApiExtension
//{
//    public static Silk.NET.WebGPU.Instance* NativeHandle(this GPUInstance<WebGPUGraphicsApi, nint> instance) => (Silk.NET.WebGPU.Instance*)instance.Handle;
//    public static Silk.NET.WebGPU.Adapter* NativeHandle(this GPUAdapter<WebGPUGraphicsApi, nint> adapter) => (Silk.NET.WebGPU.Instance*)instance.Handle;
//}
