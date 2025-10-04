using DualDrill.Common.Interop;
using Evergine.Bindings.WebGPU;
namespace DualDrill.Graphics.Backend;
using static Evergine.Bindings.WebGPU.WebGPUNative;
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
        var markerLabel_native_string = InteropUtf8String.Create(markerLabel);
        using var markerLabel_native_pined_string = markerLabel_native_string.Pin();
        wgpuCommandEncoderInsertDebugMarker(ToNative(handle.Handle), (char*)markerLabel_native_pined_string.Pointer);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPUCommandEncoder<Backend> handle)
    {
        wgpuCommandEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPUCommandEncoder<Backend> handle, string groupLabel)
    {
        var groupLabel_native_string = InteropUtf8String.Create(groupLabel);
        using var groupLabel_native_pined_string = groupLabel_native_string.Pin();
        wgpuCommandEncoderPushDebugGroup(ToNative(handle.Handle), (char*)groupLabel_native_pined_string.Pointer);
    }

    unsafe void IBackend<Backend>.ResolveQuerySet(GPUCommandEncoder<Backend> handle, GPUQuerySet<Backend> querySet, uint firstQuery, uint queryCount, GPUBuffer<Backend> destination, ulong destinationOffset)
    {
        wgpuCommandEncoderResolveQuerySet(ToNative(handle.Handle), ToNative(querySet.Handle), firstQuery, queryCount, ToNative(destination.Handle), destinationOffset);
    }

    void IGPUHandleDisposer<Backend, GPUComputePassEncoder<Backend>>.DisposeHandle(GPUHandle<Backend, GPUComputePassEncoder<Backend>> handle)
    {
        wgpuComputePassEncoderRelease(ToNative(handle));
    }

    WGPUComputePassEncoder ToNative(GPUHandle<Backend, GPUComputePassEncoder<Backend>> instance)
        => new(instance.Pointer);

    unsafe void IBackend<Backend>.DispatchWorkgroups(GPUComputePassEncoder<Backend> handle, uint workgroupCountX, uint workgroupCountY, uint workgroupCountZ)
    {
        wgpuComputePassEncoderDispatchWorkgroups(ToNative(handle.Handle), workgroupCountX, workgroupCountY, workgroupCountZ);
    }

    unsafe void IBackend<Backend>.DispatchWorkgroupsIndirect(GPUComputePassEncoder<Backend> handle, GPUBuffer<Backend> indirectBuffer, ulong indirectOffset)
    {
        wgpuComputePassEncoderDispatchWorkgroupsIndirect(ToNative(handle.Handle), ToNative(indirectBuffer.Handle), indirectOffset);
    }

    unsafe void IBackend<Backend>.End(GPUComputePassEncoder<Backend> handle)
    {
        wgpuComputePassEncoderEnd(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.InsertDebugMarker(GPUComputePassEncoder<Backend> handle, string markerLabel)
    {
        var markerLabel_native_string = InteropUtf8String.Create(markerLabel);
        using var markerLabel_native_pined_string = markerLabel_native_string.Pin();
        wgpuComputePassEncoderInsertDebugMarker(ToNative(handle.Handle), (char*)markerLabel_native_pined_string.Pointer);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPUComputePassEncoder<Backend> handle)
    {
        wgpuComputePassEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPUComputePassEncoder<Backend> handle, string groupLabel)
    {
        var groupLabel_native_string = InteropUtf8String.Create(groupLabel);
        using var groupLabel_native_pined_string = groupLabel_native_string.Pin();
        wgpuComputePassEncoderPushDebugGroup(ToNative(handle.Handle), (char*)groupLabel_native_pined_string.Pointer);
    }

    unsafe void IBackend<Backend>.SetPipeline(GPUComputePassEncoder<Backend> handle, GPUComputePipeline<Backend> pipeline)
    {
        wgpuComputePassEncoderSetPipeline(ToNative(handle.Handle), ToNative(pipeline.Handle));
    }

    void IGPUHandleDisposer<Backend, GPUComputePipeline<Backend>>.DisposeHandle(GPUHandle<Backend, GPUComputePipeline<Backend>> handle)
    {
        wgpuComputePipelineRelease(ToNative(handle));
    }

    WGPUComputePipeline ToNative(GPUHandle<Backend, GPUComputePipeline<Backend>> instance)
        => new(instance.Pointer);

    void IGPUHandleDisposer<Backend, GPUDevice<Backend>>.DisposeHandle(GPUHandle<Backend, GPUDevice<Backend>> handle)
    {
        wgpuDeviceRelease(ToNative(handle));
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
        var markerLabel_native_string = InteropUtf8String.Create(markerLabel);
        using var markerLabel_native_pined_string = markerLabel_native_string.Pin();
        wgpuRenderBundleEncoderInsertDebugMarker(ToNative(handle.Handle), (char*)markerLabel_native_pined_string.Pointer);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPURenderBundleEncoder<Backend> handle)
    {
        wgpuRenderBundleEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPURenderBundleEncoder<Backend> handle, string groupLabel)
    {
        var groupLabel_native_string = InteropUtf8String.Create(groupLabel);
        using var groupLabel_native_pined_string = groupLabel_native_string.Pin();
        wgpuRenderBundleEncoderPushDebugGroup(ToNative(handle.Handle), (char*)groupLabel_native_pined_string.Pointer);
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
        var markerLabel_native_string = InteropUtf8String.Create(markerLabel);
        using var markerLabel_native_pined_string = markerLabel_native_string.Pin();
        wgpuRenderPassEncoderInsertDebugMarker(ToNative(handle.Handle), (char*)markerLabel_native_pined_string.Pointer);
    }

    unsafe void IBackend<Backend>.PopDebugGroup(GPURenderPassEncoder<Backend> handle)
    {
        wgpuRenderPassEncoderPopDebugGroup(ToNative(handle.Handle));
    }

    unsafe void IBackend<Backend>.PushDebugGroup(GPURenderPassEncoder<Backend> handle, string groupLabel)
    {
        var groupLabel_native_string = InteropUtf8String.Create(groupLabel);
        using var groupLabel_native_pined_string = groupLabel_native_string.Pin();
        wgpuRenderPassEncoderPushDebugGroup(ToNative(handle.Handle), (char*)groupLabel_native_pined_string.Pointer);
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
        => (WGPUAddressMode)(value);

    WGPUBlendFactor ToNative(GPUBlendFactor value)
        => (WGPUBlendFactor)(value);

    WGPUBlendOperation ToNative(GPUBlendOperation value)
        => (WGPUBlendOperation)(value);

    WGPUBufferBindingType ToNative(GPUBufferBindingType value)
        => (WGPUBufferBindingType)(value);

    WGPUBufferMapState ToNative(GPUBufferMapState value)
        => (WGPUBufferMapState)(value);

    WGPUBufferUsage ToNative(GPUBufferUsage value)
        => (WGPUBufferUsage)(value);

    WGPUColorWriteMask ToNative(GPUColorWriteMask value)
        => (WGPUColorWriteMask)(value);

    WGPUCompareFunction ToNative(GPUCompareFunction value)
        => (WGPUCompareFunction)(value);

    WGPUCompilationMessageType ToNative(GPUCompilationMessageType value)
        => (WGPUCompilationMessageType)(value);

    WGPUCullMode ToNative(GPUCullMode value)
        => (WGPUCullMode)(value);

    WGPUDeviceLostReason ToNative(GPUDeviceLostReason value)
        => (WGPUDeviceLostReason)(value);

    WGPUErrorFilter ToNative(GPUErrorFilter value)
        => (WGPUErrorFilter)(value);

    WGPUFeatureName ToNative(GPUFeatureName value)
        => (WGPUFeatureName)(value);

    WGPUFilterMode ToNative(GPUFilterMode value)
        => (WGPUFilterMode)(value);

    WGPUFrontFace ToNative(GPUFrontFace value)
        => (WGPUFrontFace)(value);

    WGPUIndexFormat ToNative(GPUIndexFormat value)
        => (WGPUIndexFormat)(value);

    WGPULoadOp ToNative(GPULoadOp value)
        => (WGPULoadOp)(value);

    WGPUMapMode ToNative(GPUMapMode value)
        => (WGPUMapMode)(value);

    WGPUMipmapFilterMode ToNative(GPUMipmapFilterMode value)
        => (WGPUMipmapFilterMode)(value);

    WGPUPowerPreference ToNative(GPUPowerPreference value)
        => (WGPUPowerPreference)(value);

    WGPUPrimitiveTopology ToNative(GPUPrimitiveTopology value)
        => (WGPUPrimitiveTopology)(value);

    WGPUQueryType ToNative(GPUQueryType value)
        => (WGPUQueryType)(value);

    WGPUSamplerBindingType ToNative(GPUSamplerBindingType value)
        => (WGPUSamplerBindingType)(value);

    WGPUShaderStage ToNative(GPUShaderStage value)
        => (WGPUShaderStage)(value);

    WGPUStencilOperation ToNative(GPUStencilOperation value)
        => (WGPUStencilOperation)(value);

    WGPUStorageTextureAccess ToNative(GPUStorageTextureAccess value)
        => (WGPUStorageTextureAccess)(value);

    WGPUStoreOp ToNative(GPUStoreOp value)
        => (WGPUStoreOp)(value);

    WGPUTextureAspect ToNative(GPUTextureAspect value)
        => (WGPUTextureAspect)(value);

    WGPUTextureDimension ToNative(GPUTextureDimension value)
        => (WGPUTextureDimension)(value);

    WGPUTextureFormat ToNative(GPUTextureFormat value)
        => (WGPUTextureFormat)(value);

    WGPUTextureSampleType ToNative(GPUTextureSampleType value)
        => (WGPUTextureSampleType)(value);

    WGPUTextureUsage ToNative(GPUTextureUsage value)
        => (WGPUTextureUsage)(value);

    WGPUTextureViewDimension ToNative(GPUTextureViewDimension value)
        => (WGPUTextureViewDimension)(value);

    WGPUVertexFormat ToNative(GPUVertexFormat value)
        => (WGPUVertexFormat)(value);

    WGPUVertexStepMode ToNative(GPUVertexStepMode value)
        => (WGPUVertexStepMode)(value);

}

