using WebGPU;
namespace DualDrill.Graphics.Backend;

using static WebGPU.WebGPU;
using Backend = DualDrill.Graphics.Backend.WebGPUNETBackend;

public sealed partial class WebGPUNETBackend
{
    void IGPUHandleDisposer<Backend, GPUAdapter<Backend>>.DisposeHandle(GPUHandle<Backend, GPUAdapter<Backend>> handle)
    {
        wgpuAdapterRelease(ToNative(handle));
    }

    WGPUAdapter ToNative(GPUHandle<Backend, GPUAdapter<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUBindGroup<Backend>>.DisposeHandle(GPUHandle<Backend, GPUBindGroup<Backend>> handle)
    {
        wgpuBindGroupRelease(ToNative(handle));
    }

    WGPUBindGroup ToNative(GPUHandle<Backend, GPUBindGroup<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUBindGroupLayout<Backend>>.DisposeHandle(GPUHandle<Backend, GPUBindGroupLayout<Backend>> handle)
    {
        wgpuBindGroupLayoutRelease(ToNative(handle));
    }

    WGPUBindGroupLayout ToNative(GPUHandle<Backend, GPUBindGroupLayout<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUBuffer<Backend>>.DisposeHandle(GPUHandle<Backend, GPUBuffer<Backend>> handle)
    {
        wgpuBufferRelease(ToNative(handle));
    }

    WGPUBuffer ToNative(GPUHandle<Backend, GPUBuffer<Backend>> instance)
        => new(instance.Pointer);


    unsafe void IBackend<Backend>.Unmap(GPUBuffer<Backend> handle)
    {
        wgpuBufferUnmap(ToNative(handle.Handle));
    }

    void IGPUHandleDisposer<Backend, GPUCommandBuffer<Backend>>.DisposeHandle(GPUHandle<Backend, GPUCommandBuffer<Backend>> handle)
    {
        wgpuCommandBufferRelease(ToNative(handle));
    }

    WGPUCommandBuffer ToNative(GPUHandle<Backend, GPUCommandBuffer<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUCommandEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPUCommandEncoder<Backend>> handle)
    {
        wgpuCommandEncoderRelease(ToNative(handle));
    }

    WGPUCommandEncoder ToNative(GPUHandle<Backend, GPUCommandEncoder<Backend>> instance)
        => new(instance.Pointer);

    unsafe void IBackend<Backend>.ClearBuffer(GPUCommandEncoder<Backend> handle, GPUBuffer<Backend> buffer, ulong offset, ulong size)
    {
        wgpuCommandEncoderClearBuffer(ToNative(handle.Handle), ToNative(buffer.Handle), offset, size);
    }

    unsafe void IBackend<Backend>.CopyBufferToBuffer(GPUCommandEncoder<Backend> handle, GPUBuffer<Backend> source, ulong sourceOffset, GPUBuffer<Backend> destination, ulong destinationOffset, ulong size)
    {
        wgpuCommandEncoderCopyBufferToBuffer(ToNative(handle.Handle), ToNative(source.Handle), sourceOffset, ToNative(destination.Handle), destinationOffset, size);
    }

    unsafe void IBackend<Backend>.InsertDebugMarker(GPUCommandEncoder<Backend> handle, string markerLabel)
    {
        wgpuCommandEncoderInsertDebugMarker(ToNative(handle.Handle), markerLabel);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPUCommandEncoder<Backend> handle)
    {
        wgpuCommandEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPUCommandEncoder<Backend> handle, string groupLabel)
    {
        wgpuCommandEncoderPushDebugGroup(ToNative(handle.Handle), groupLabel);
    }

    unsafe void IBackend<Backend>.ResolveQuerySet(GPUCommandEncoder<Backend> handle, GPUQuerySet<Backend> querySet, uint firstQuery, uint queryCount, GPUBuffer<Backend> destination, ulong destinationOffset)
    {
        wgpuCommandEncoderResolveQuerySet(ToNative(handle.Handle), ToNative(querySet.Handle), firstQuery, queryCount, ToNative(destination.Handle), destinationOffset);
    }

    WGPUComputePassEncoder ToNative(GPUHandle<Backend, GPUComputePassEncoder<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUComputePipeline<Backend>>.DisposeHandle(GPUHandle<Backend, GPUComputePipeline<Backend>> handle)
    {
        wgpuComputePipelineRelease(ToNative(handle));
    }

    WGPUComputePipeline ToNative(GPUHandle<Backend, GPUComputePipeline<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUDevice<Backend>>.DisposeHandle(GPUHandle<Backend, GPUDevice<Backend>> handle)
    {
        wgpuDeviceRelease(ToNative(handle));
        if (handle.Data is DeviceState state)
        {
            wgpuInstanceProcessEvents(state.Instance.Instance);
            state.Dispose();
        }
    }

    WGPUDevice ToNative(GPUHandle<Backend, GPUDevice<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUInstance<Backend>>.DisposeHandle(GPUHandle<Backend, GPUInstance<Backend>> handle)
    {
        wgpuInstanceRelease(ToNative(handle));
    }

    WGPUInstance ToNative(GPUHandle<Backend, GPUInstance<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUPipelineLayout<Backend>>.DisposeHandle(GPUHandle<Backend, GPUPipelineLayout<Backend>> handle)
    {
        wgpuPipelineLayoutRelease(ToNative(handle));
    }

    WGPUPipelineLayout ToNative(GPUHandle<Backend, GPUPipelineLayout<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUQuerySet<Backend>>.DisposeHandle(GPUHandle<Backend, GPUQuerySet<Backend>> handle)
    {
        wgpuQuerySetRelease(ToNative(handle));
    }

    WGPUQuerySet ToNative(GPUHandle<Backend, GPUQuerySet<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUQueue<Backend>>.DisposeHandle(GPUHandle<Backend, GPUQueue<Backend>> handle)
    {
        wgpuQueueRelease(ToNative(handle));
    }

    WGPUQueue ToNative(GPUHandle<Backend, GPUQueue<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPURenderBundle<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderBundle<Backend>> handle)
    {
        wgpuRenderBundleRelease(ToNative(handle));
    }

    WGPURenderBundle ToNative(GPUHandle<Backend, GPURenderBundle<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPURenderBundleEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderBundleEncoder<Backend>> handle)
    {
        wgpuRenderBundleEncoderRelease(ToNative(handle));
    }

    WGPURenderBundleEncoder ToNative(GPUHandle<Backend, GPURenderBundleEncoder<Backend>> instance)
        => new(instance.Pointer);

    unsafe void IBackend<Backend>.Draw(GPURenderBundleEncoder<Backend> handle, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        wgpuRenderBundleEncoderDraw(ToNative(handle.Handle), vertexCount, instanceCount, firstVertex, firstInstance);
    }

    unsafe void IBackend<Backend>.DrawIndexed(GPURenderBundleEncoder<Backend> handle, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
    {
        wgpuRenderBundleEncoderDrawIndexed(ToNative(handle.Handle), indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }

    unsafe void IBackend<Backend>.DrawIndexedIndirect(GPURenderBundleEncoder<Backend> handle, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
    }

    unsafe void IBackend<Backend>.DrawIndirect(GPURenderBundleEncoder<Backend> handle, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
    }

    unsafe void IBackend<Backend>.InsertDebugMarker(GPURenderBundleEncoder<Backend> handle, string markerLabel)
    {
        wgpuRenderBundleEncoderInsertDebugMarker(ToNative(handle.Handle), markerLabel);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPURenderBundleEncoder<Backend> handle)
    {
        wgpuRenderBundleEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPURenderBundleEncoder<Backend> handle, string groupLabel)
    {
        wgpuRenderBundleEncoderPushDebugGroup(ToNative(handle.Handle), groupLabel);
    }

    unsafe void IBackend<Backend>.SetPipeline(GPURenderBundleEncoder<Backend> handle, GPURenderPipeline<Backend> pipeline)
    {
        wgpuRenderBundleEncoderSetPipeline(ToNative(handle.Handle), ToNative(pipeline.Handle));
    }

    void IGPUHandleDisposer<Backend, GPURenderPassEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderPassEncoder<Backend>> handle)
    {
        wgpuRenderPassEncoderRelease(ToNative(handle));
    }

    WGPURenderPassEncoder ToNative(GPUHandle<Backend, GPURenderPassEncoder<Backend>> instance)
        => new(instance.Pointer);

    unsafe void IBackend<Backend>.BeginOcclusionQuery(GPURenderPassEncoder<Backend> handle, uint queryIndex)
    {
        wgpuRenderPassEncoderBeginOcclusionQuery(ToNative(handle.Handle), queryIndex);
    }

    unsafe void IBackend<Backend>.Draw(GPURenderPassEncoder<Backend> handle, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        wgpuRenderPassEncoderDraw(ToNative(handle.Handle), vertexCount, instanceCount, firstVertex, firstInstance);
    }

    unsafe void IBackend<Backend>.DrawIndexed(GPURenderPassEncoder<Backend> handle, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
    {
        wgpuRenderPassEncoderDrawIndexed(ToNative(handle.Handle), indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }

    unsafe void IBackend<Backend>.DrawIndexedIndirect(GPURenderPassEncoder<Backend> handle, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
        wgpuRenderPassEncoderDrawIndexedIndirect(ToNative(handle.Handle), ToNative(indirectBuffer.Handle), indirectOffset);
    }

    unsafe void IBackend<Backend>.DrawIndirect(GPURenderPassEncoder<Backend> handle, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
        wgpuRenderPassEncoderDrawIndirect(ToNative(handle.Handle), ToNative(indirectBuffer.Handle), indirectOffset);
    }

    unsafe void IBackend<Backend>.End(GPURenderPassEncoder<Backend> handle)
    {
        wgpuRenderPassEncoderEnd(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.EndOcclusionQuery(GPURenderPassEncoder<Backend> handle)
    {
        wgpuRenderPassEncoderEndOcclusionQuery(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.InsertDebugMarker(GPURenderPassEncoder<Backend> handle, string markerLabel)
    {
        wgpuRenderPassEncoderInsertDebugMarker(ToNative(handle.Handle), markerLabel);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPURenderPassEncoder<Backend> handle)
    {
        wgpuRenderPassEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPURenderPassEncoder<Backend> handle, string groupLabel)
    {
        wgpuRenderPassEncoderPushDebugGroup(ToNative(handle.Handle), groupLabel);
    }

    unsafe void IBackend<Backend>.SetPipeline(GPURenderPassEncoder<Backend> handle, GPURenderPipeline<Backend> pipeline)
    {
        wgpuRenderPassEncoderSetPipeline(ToNative(handle.Handle), ToNative(pipeline.Handle));
    }

    unsafe void IBackend<Backend>.SetScissorRect(GPURenderPassEncoder<Backend> handle, uint x, uint y, uint width, uint height)
    {
        wgpuRenderPassEncoderSetScissorRect(ToNative(handle.Handle), x, y, width, height);
    }

    unsafe void IBackend<Backend>.SetStencilReference(GPURenderPassEncoder<Backend> handle, uint reference)
    {
        wgpuRenderPassEncoderSetStencilReference(ToNative(handle.Handle), reference);
    }

    void IGPUHandleDisposer<Backend, GPURenderPipeline<Backend>>.DisposeHandle(GPUHandle<Backend, GPURenderPipeline<Backend>> handle)
    {
        wgpuRenderPipelineRelease(ToNative(handle));
    }

    WGPURenderPipeline ToNative(GPUHandle<Backend, GPURenderPipeline<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUSampler<Backend>>.DisposeHandle(GPUHandle<Backend, GPUSampler<Backend>> handle)
    {
        wgpuSamplerRelease(ToNative(handle));
    }

    WGPUSampler ToNative(GPUHandle<Backend, GPUSampler<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUShaderModule<Backend>>.DisposeHandle(GPUHandle<Backend, GPUShaderModule<Backend>> handle)
    {
        wgpuShaderModuleRelease(ToNative(handle));
    }

    WGPUShaderModule ToNative(GPUHandle<Backend, GPUShaderModule<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUSurface<Backend>>.DisposeHandle(GPUHandle<Backend, GPUSurface<Backend>> handle)
    {
        wgpuSurfaceRelease(ToNative(handle));
    }

    WGPUSurface ToNative(GPUHandle<Backend, GPUSurface<Backend>> instance)
        => new(instance.Pointer);

    unsafe void IBackend<Backend>.Unconfigure(GPUSurface<Backend> handle)
    {
        wgpuSurfaceUnconfigure(ToNative(handle.Handle));
    }

    void IGPUHandleDisposer<Backend, GPUTexture<Backend>>.DisposeHandle(GPUHandle<Backend, GPUTexture<Backend>> handle)
    {
        wgpuTextureRelease(ToNative(handle));
    }

    WGPUTexture ToNative(GPUHandle<Backend, GPUTexture<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUTextureView<Backend>>.DisposeHandle(GPUHandle<Backend, GPUTextureView<Backend>> handle)
    {
        wgpuTextureViewRelease(ToNative(handle));
    }

    WGPUTextureView ToNative(GPUHandle<Backend, GPUTextureView<Backend>> instance)
        => new(instance.Pointer);

    WGPUAddressMode ToNative(GPUAddressMode value)
        => MapEnumByName<GPUAddressMode, WGPUAddressMode>(value);

    WGPUBlendFactor ToNative(GPUBlendFactor value)
        => MapEnumByName<GPUBlendFactor, WGPUBlendFactor>(value);

    WGPUBlendOperation ToNative(GPUBlendOperation value)
        => MapEnumByName<GPUBlendOperation, WGPUBlendOperation>(value);

    WGPUBufferBindingType ToNative(GPUBufferBindingType value)
        => (int)value == 0
            ? WGPUBufferBindingType.BindingNotUsed
            : MapEnumByName<GPUBufferBindingType, WGPUBufferBindingType>(value);

    WGPUBufferMapState ToNative(GPUBufferMapState value)
        => MapEnumByName<GPUBufferMapState, WGPUBufferMapState>(value);

    WGPUBufferUsage ToNative(GPUBufferUsage value)
        => MapEnumByName<GPUBufferUsage, WGPUBufferUsage>(value);

    WGPUColorWriteMask ToNative(GPUColorWriteMask value)
        => MapEnumByName<GPUColorWriteMask, WGPUColorWriteMask>(value);

    WGPUCompareFunction ToNative(GPUCompareFunction value)
        => (int)value == 0
            ? WGPUCompareFunction.Undefined
            : MapEnumByName<GPUCompareFunction, WGPUCompareFunction>(value);

    WGPUCompilationMessageType ToNative(GPUCompilationMessageType value)
        => MapEnumByName<GPUCompilationMessageType, WGPUCompilationMessageType>(value);

    WGPUCullMode ToNative(GPUCullMode value)
        => MapEnumByName<GPUCullMode, WGPUCullMode>(value);

    WGPUDeviceLostReason ToNative(GPUDeviceLostReason value)
        => value switch
        {
            GPUDeviceLostReason.Undefined => WGPUDeviceLostReason.Unknown,
            GPUDeviceLostReason.Destroyed => WGPUDeviceLostReason.Destroyed,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };

    WGPUErrorFilter ToNative(GPUErrorFilter value)
        => MapEnumByName<GPUErrorFilter, WGPUErrorFilter>(value);

    WGPUFeatureName ToNative(GPUFeatureName value)
        => MapEnumByName<GPUFeatureName, WGPUFeatureName>(value);

    WGPUFilterMode ToNative(GPUFilterMode value)
        => MapEnumByName<GPUFilterMode, WGPUFilterMode>(value);

    WGPUFrontFace ToNative(GPUFrontFace value)
        => MapEnumByName<GPUFrontFace, WGPUFrontFace>(value);

    WGPUIndexFormat ToNative(GPUIndexFormat value)
        => (int)value == 0
            ? WGPUIndexFormat.Undefined
            : MapEnumByName<GPUIndexFormat, WGPUIndexFormat>(value);

    WGPULoadOp ToNative(GPULoadOp value)
        => (int)value == 0
            ? WGPULoadOp.Undefined
            : MapEnumByName<GPULoadOp, WGPULoadOp>(value);

    WGPUMapMode ToNative(GPUMapMode value)
        => MapEnumByName<GPUMapMode, WGPUMapMode>(value);

    WGPUMipmapFilterMode ToNative(GPUMipmapFilterMode value)
        => MapEnumByName<GPUMipmapFilterMode, WGPUMipmapFilterMode>(value);

    WGPUPowerPreference ToNative(GPUPowerPreference value)
        => (int)value == 0
            ? WGPUPowerPreference.Undefined
            : MapEnumByName<GPUPowerPreference, WGPUPowerPreference>(value);

    WGPUPrimitiveTopology ToNative(GPUPrimitiveTopology value)
        => MapEnumByName<GPUPrimitiveTopology, WGPUPrimitiveTopology>(value);

    WGPUQueryType ToNative(GPUQueryType value)
        => MapEnumByName<GPUQueryType, WGPUQueryType>(value);

    WGPUSamplerBindingType ToNative(GPUSamplerBindingType value)
        => (int)value == 0
            ? WGPUSamplerBindingType.BindingNotUsed
            : MapEnumByName<GPUSamplerBindingType, WGPUSamplerBindingType>(value);

    WGPUShaderStage ToNative(GPUShaderStage value)
        => MapEnumByName<GPUShaderStage, WGPUShaderStage>(value);

    WGPUStencilOperation ToNative(GPUStencilOperation value)
        => MapEnumByName<GPUStencilOperation, WGPUStencilOperation>(value);

    WGPUStorageTextureAccess ToNative(GPUStorageTextureAccess value)
        => (int)value == 0
            ? WGPUStorageTextureAccess.BindingNotUsed
            : MapEnumByName<GPUStorageTextureAccess, WGPUStorageTextureAccess>(value);

    WGPUStoreOp ToNative(GPUStoreOp value)
        => (int)value == 0
            ? WGPUStoreOp.Undefined
            : MapEnumByName<GPUStoreOp, WGPUStoreOp>(value);

    WGPUTextureAspect ToNative(GPUTextureAspect value)
        => MapEnumByName<GPUTextureAspect, WGPUTextureAspect>(value);

    WGPUTextureDimension ToNative(GPUTextureDimension value)
        => MapEnumByName<GPUTextureDimension, WGPUTextureDimension>(value);

    WGPUTextureFormat ToNative(GPUTextureFormat value)
        => (int)value == 0
            ? WGPUTextureFormat.Undefined
            : MapEnumByName<GPUTextureFormat, WGPUTextureFormat>(value);

    WGPUTextureSampleType ToNative(GPUTextureSampleType value)
        => (int)value == 0
            ? WGPUTextureSampleType.BindingNotUsed
            : MapEnumByName<GPUTextureSampleType, WGPUTextureSampleType>(value);

    WGPUTextureUsage ToNative(GPUTextureUsage value)
        => MapEnumByName<GPUTextureUsage, WGPUTextureUsage>(value);

    WGPUTextureViewDimension ToNative(GPUTextureViewDimension value)
        => (int)value == 0
            ? WGPUTextureViewDimension.Undefined
            : MapEnumByName<GPUTextureViewDimension, WGPUTextureViewDimension>(value);

    WGPUVertexFormat ToNative(GPUVertexFormat value)
        => MapEnumByName<GPUVertexFormat, WGPUVertexFormat>(value);

    WGPUVertexStepMode ToNative(GPUVertexStepMode value)
        => MapEnumByName<GPUVertexStepMode, WGPUVertexStepMode>(value);

}
