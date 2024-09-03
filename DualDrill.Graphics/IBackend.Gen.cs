using System;
using System.Collections.Immutable;

namespace DualDrill.Graphics;
public partial interface IBackend<TBackend>
 : IDisposable
    , IGPUHandleDisposer<TBackend, GPUAdapter<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUBindGroup<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUBindGroupLayout<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUBuffer<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUCommandBuffer<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUCommandEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUComputePassEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUComputePipeline<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUDevice<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUInstance<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUPipelineLayout<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUQuerySet<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUQueue<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderBundle<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderBundleEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderPassEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderPipeline<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUSampler<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUShaderModule<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUSurface<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUTexture<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUTextureView<TBackend>>
{
    #region GPUAdapter methods

    internal ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(
        GPUAdapter<TBackend> handle,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation);

    #endregion

    #region GPUBuffer methods

    internal ValueTask MapAsyncAsync(
        GPUBuffer<TBackend> handle,
        GPUMapMode mode,
        ulong offset,
        ulong size,
        CancellationToken cancellation);

    internal ReadOnlySpan<byte> GetMappedRange(
        GPUBuffer<TBackend> handle,
        ulong offset,
        ulong size);

    internal void Unmap(
        GPUBuffer<TBackend> handle);

    #endregion

    #region GPUCommandEncoder methods

    internal GPURenderPassEncoder<TBackend> BeginRenderPass(
        GPUCommandEncoder<TBackend> handle,
        GPURenderPassDescriptor descriptor);

    internal GPUComputePassEncoder<TBackend> BeginComputePass(
        GPUCommandEncoder<TBackend> handle,
        GPUComputePassDescriptor descriptor);

    internal void CopyBufferToBuffer(
        GPUCommandEncoder<TBackend> handle,
        GPUBuffer<TBackend> source,
        ulong sourceOffset,
        GPUBuffer<TBackend> destination,
        ulong destinationOffset,
        ulong size);

    internal void CopyBufferToTexture(
        GPUCommandEncoder<TBackend> handle,
        GPUImageCopyBuffer source,
        GPUImageCopyTexture destination,
        GPUExtent3D copySize);

    internal void CopyTextureToBuffer(
        GPUCommandEncoder<TBackend> handle,
        GPUImageCopyTexture source,
        GPUImageCopyBuffer destination,
        GPUExtent3D copySize);

    internal void CopyTextureToTexture(
        GPUCommandEncoder<TBackend> handle,
        GPUImageCopyTexture source,
        GPUImageCopyTexture destination,
        GPUExtent3D copySize);

    internal void ClearBuffer(
        GPUCommandEncoder<TBackend> handle,
        GPUBuffer<TBackend> buffer,
        ulong offset,
        ulong size);

    internal void ResolveQuerySet(
        GPUCommandEncoder<TBackend> handle,
        GPUQuerySet<TBackend> querySet,
        uint firstQuery,
        uint queryCount,
        GPUBuffer<TBackend> destination,
        ulong destinationOffset);

    internal GPUCommandBuffer<TBackend> Finish(
        GPUCommandEncoder<TBackend> handle,
        GPUCommandBufferDescriptor descriptor);

    internal void PushDebugGroup(
        GPUCommandEncoder<TBackend> handle,
        string groupLabel);

    internal void PopDebugGroup(
        GPUCommandEncoder<TBackend> handle);

    internal void InsertDebugMarker(
        GPUCommandEncoder<TBackend> handle,
        string markerLabel);

    #endregion

    #region GPUComputePassEncoder methods

    internal void SetBindGroup(
        GPUComputePassEncoder<TBackend> handle,
        int index,
        GPUBindGroup<TBackend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsets);

    internal void SetBindGroup(
        GPUComputePassEncoder<TBackend> handle,
        int index,
        GPUBindGroup<TBackend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsetsData,
        ulong dynamicOffsetsDataStart,
        uint dynamicOffsetsDataLength);

    internal void PushDebugGroup(
        GPUComputePassEncoder<TBackend> handle,
        string groupLabel);

    internal void PopDebugGroup(
        GPUComputePassEncoder<TBackend> handle);

    internal void InsertDebugMarker(
        GPUComputePassEncoder<TBackend> handle,
        string markerLabel);

    internal void SetPipeline(
        GPUComputePassEncoder<TBackend> handle,
        GPUComputePipeline<TBackend> pipeline);

    internal void DispatchWorkgroups(
        GPUComputePassEncoder<TBackend> handle,
        uint workgroupCountX,
        uint workgroupCountY,
        uint workgroupCountZ);

    internal void DispatchWorkgroupsIndirect(
        GPUComputePassEncoder<TBackend> handle,
        GPUBuffer<TBackend> indirectBuffer,
        ulong indirectOffset);

    internal void End(
        GPUComputePassEncoder<TBackend> handle);

    #endregion

    #region GPUDevice methods

    internal GPUBuffer<TBackend> CreateBuffer(
        GPUDevice<TBackend> handle,
        GPUBufferDescriptor descriptor);

    internal GPUTexture<TBackend> CreateTexture(
        GPUDevice<TBackend> handle,
        GPUTextureDescriptor descriptor);

    internal GPUSampler<TBackend> CreateSampler(
        GPUDevice<TBackend> handle,
        GPUSamplerDescriptor descriptor);

    internal GPUBindGroupLayout<TBackend> CreateBindGroupLayout(
        GPUDevice<TBackend> handle,
        GPUBindGroupLayoutDescriptor descriptor);

    internal GPUPipelineLayout<TBackend> CreatePipelineLayout(
        GPUDevice<TBackend> handle,
        GPUPipelineLayoutDescriptor descriptor);

    internal GPUBindGroup<TBackend> CreateBindGroup(
        GPUDevice<TBackend> handle,
        GPUBindGroupDescriptor descriptor);

    internal GPUShaderModule<TBackend> CreateShaderModule(
        GPUDevice<TBackend> handle,
        GPUShaderModuleDescriptor descriptor);

    internal GPUComputePipeline<TBackend> CreateComputePipeline(
        GPUDevice<TBackend> handle,
        GPUComputePipelineDescriptor descriptor);

    internal GPURenderPipeline<TBackend> CreateRenderPipeline(
        GPUDevice<TBackend> handle,
        GPURenderPipelineDescriptor descriptor);

    internal ValueTask<GPUComputePipeline<TBackend>> CreateComputePipelineAsyncAsync(
        GPUDevice<TBackend> handle,
        GPUComputePipelineDescriptor descriptor,
        CancellationToken cancellation);

    internal ValueTask<GPURenderPipeline<TBackend>> CreateRenderPipelineAsyncAsync(
        GPUDevice<TBackend> handle,
        GPURenderPipelineDescriptor descriptor,
        CancellationToken cancellation);

    internal GPUCommandEncoder<TBackend> CreateCommandEncoder(
        GPUDevice<TBackend> handle,
        GPUCommandEncoderDescriptor descriptor);

    internal GPURenderBundleEncoder<TBackend> CreateRenderBundleEncoder(
        GPUDevice<TBackend> handle,
        GPURenderBundleEncoderDescriptor descriptor);

    internal GPUQuerySet<TBackend> CreateQuerySet(
        GPUDevice<TBackend> handle,
        GPUQuerySetDescriptor descriptor);

    #endregion

    #region GPUInstance methods

    internal ValueTask<GPUAdapter<TBackend>?> RequestAdapterAsync(
        GPUInstance<TBackend> handle,
        GPURequestAdapterOptions options,
        CancellationToken cancellation);

    internal GPUTextureFormat GetPreferredCanvasFormat(
        GPUInstance<TBackend> handle);

    #endregion

}

