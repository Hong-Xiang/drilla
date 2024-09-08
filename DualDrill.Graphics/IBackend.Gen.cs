using System;
using System.Collections.Immutable;


namespace DualDrill.Graphics;
public partial interface IBackend<TBackend>
 : IDisposable
    , IGPUHandleDisposer<TBackend, GPUQueue<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUCommandEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUDevice<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUBuffer<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUCommandBuffer<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUSurface<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderPipeline<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUQuerySet<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderPassEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUBindGroup<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUComputePassEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUBindGroupLayout<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUPipelineLayout<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderBundle<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUTexture<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUSampler<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUTextureView<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUShaderModule<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUComputePipeline<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUAdapter<TBackend>>
    , IGPUHandleDisposer<TBackend, GPURenderBundleEncoder<TBackend>>
    , IGPUHandleDisposer<TBackend, GPUInstance<TBackend>>
{
    #region GPUAdapter methods

    internal ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(
        GPUAdapter<TBackend> handle,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation);

    #endregion

    #region GPUBuffer methods

    internal ReadOnlySpan<byte> GetMappedRange(
        GPUBuffer<TBackend> handle,
        ulong offset,
        ulong size);

    internal ValueTask MapAsyncAsync(
        GPUBuffer<TBackend> handle,
        GPUMapMode mode,
        ulong offset,
        ulong size,
        CancellationToken cancellation);

    internal void Unmap(
        GPUBuffer<TBackend> handle);

    #endregion

    #region GPUCommandEncoder methods

    internal GPUComputePassEncoder<TBackend> BeginComputePass(
        GPUCommandEncoder<TBackend> handle,
        GPUComputePassDescriptor descriptor);

    internal GPURenderPassEncoder<TBackend> BeginRenderPass(
        GPUCommandEncoder<TBackend> handle,
        GPURenderPassDescriptor descriptor);

    internal void ClearBuffer(
        GPUCommandEncoder<TBackend> handle,
        GPUBuffer<TBackend> buffer,
        ulong offset,
        ulong size);

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

    internal GPUCommandBuffer<TBackend> Finish(
        GPUCommandEncoder<TBackend> handle,
        GPUCommandBufferDescriptor descriptor);

    internal void InsertDebugMarker(
        GPUCommandEncoder<TBackend> handle,
        string markerLabel);

    internal void PopDebugGroup(
        GPUCommandEncoder<TBackend> handle);

    internal void PushDebugGroup(
        GPUCommandEncoder<TBackend> handle,
        string groupLabel);

    internal void ResolveQuerySet(
        GPUCommandEncoder<TBackend> handle,
        GPUQuerySet<TBackend> querySet,
        uint firstQuery,
        uint queryCount,
        GPUBuffer<TBackend> destination,
        ulong destinationOffset);

    #endregion

    #region GPUComputePassEncoder methods

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

    internal void InsertDebugMarker(
        GPUComputePassEncoder<TBackend> handle,
        string markerLabel);

    internal void PopDebugGroup(
        GPUComputePassEncoder<TBackend> handle);

    internal void PushDebugGroup(
        GPUComputePassEncoder<TBackend> handle,
        string groupLabel);

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

    internal void SetPipeline(
        GPUComputePassEncoder<TBackend> handle,
        GPUComputePipeline<TBackend> pipeline);

    #endregion

    #region GPUComputePipeline methods

    internal GPUBindGroupLayout<TBackend> GetBindGroupLayout(
        GPUComputePipeline<TBackend> handle,
        ulong index);

    #endregion

    #region GPUDevice methods

    internal GPUBindGroup<TBackend> CreateBindGroup(
        GPUDevice<TBackend> handle,
        GPUBindGroupDescriptor descriptor);

    internal GPUBindGroupLayout<TBackend> CreateBindGroupLayout(
        GPUDevice<TBackend> handle,
        GPUBindGroupLayoutDescriptor descriptor);

    internal GPUBuffer<TBackend> CreateBuffer(
        GPUDevice<TBackend> handle,
        GPUBufferDescriptor descriptor);

    internal GPUCommandEncoder<TBackend> CreateCommandEncoder(
        GPUDevice<TBackend> handle,
        GPUCommandEncoderDescriptor descriptor);

    internal GPUComputePipeline<TBackend> CreateComputePipeline(
        GPUDevice<TBackend> handle,
        GPUComputePipelineDescriptor descriptor);

    internal ValueTask<GPUComputePipeline<TBackend>> CreateComputePipelineAsyncAsync(
        GPUDevice<TBackend> handle,
        GPUComputePipelineDescriptor descriptor,
        CancellationToken cancellation);

    internal GPUPipelineLayout<TBackend> CreatePipelineLayout(
        GPUDevice<TBackend> handle,
        GPUPipelineLayoutDescriptor descriptor);

    internal GPUQuerySet<TBackend> CreateQuerySet(
        GPUDevice<TBackend> handle,
        GPUQuerySetDescriptor descriptor);

    internal GPURenderBundleEncoder<TBackend> CreateRenderBundleEncoder(
        GPUDevice<TBackend> handle,
        GPURenderBundleEncoderDescriptor descriptor);

    internal GPURenderPipeline<TBackend> CreateRenderPipeline(
        GPUDevice<TBackend> handle,
        GPURenderPipelineDescriptor descriptor);

    internal ValueTask<GPURenderPipeline<TBackend>> CreateRenderPipelineAsyncAsync(
        GPUDevice<TBackend> handle,
        GPURenderPipelineDescriptor descriptor,
        CancellationToken cancellation);

    internal GPUSampler<TBackend> CreateSampler(
        GPUDevice<TBackend> handle,
        GPUSamplerDescriptor descriptor);

    internal GPUShaderModule<TBackend> CreateShaderModule(
        GPUDevice<TBackend> handle,
        GPUShaderModuleDescriptor descriptor);

    internal GPUTexture<TBackend> CreateTexture(
        GPUDevice<TBackend> handle,
        GPUTextureDescriptor descriptor);

    #endregion

    #region GPUInstance methods

    internal GPUTextureFormat GetPreferredCanvasFormat(
        GPUInstance<TBackend> handle);

    internal ValueTask<GPUAdapter<TBackend>?> RequestAdapterAsync(
        GPUInstance<TBackend> handle,
        GPURequestAdapterOptions options,
        CancellationToken cancellation);

    #endregion

    #region GPUQueue methods

    internal ValueTask OnSubmittedWorkDoneAsync(
        GPUQueue<TBackend> handle,
        CancellationToken cancellation);

    internal void Submit(
        GPUQueue<TBackend> handle,
        ReadOnlySpan<GPUCommandBuffer<TBackend>> commandBuffers);

    internal void WriteBuffer(
        GPUQueue<TBackend> handle,
        GPUBuffer<TBackend> buffer,
        ulong bufferOffset,
        nint data,
        ulong dataOffset,
        ulong size);

    internal void WriteTexture(
        GPUQueue<TBackend> handle,
        GPUImageCopyTexture destination,
        nint data,
        GPUTextureDataLayout dataLayout,
        GPUExtent3D size);

    #endregion

    #region GPURenderBundleEncoder methods

    internal void Draw(
        GPURenderBundleEncoder<TBackend> handle,
        uint vertexCount,
        uint instanceCount,
        uint firstVertex,
        uint firstInstance);

    internal void DrawIndexed(
        GPURenderBundleEncoder<TBackend> handle,
        uint indexCount,
        uint instanceCount,
        uint firstIndex,
        int baseVertex,
        uint firstInstance);

    internal void DrawIndexedIndirect(
        GPURenderBundleEncoder<TBackend> handle,
        GPUBuffer<TBackend> indirectBuffer,
        ulong indirectOffset);

    internal void DrawIndirect(
        GPURenderBundleEncoder<TBackend> handle,
        GPUBuffer<TBackend> indirectBuffer,
        ulong indirectOffset);

    internal GPURenderBundle<TBackend> Finish(
        GPURenderBundleEncoder<TBackend> handle,
        GPURenderBundleDescriptor descriptor);

    internal void InsertDebugMarker(
        GPURenderBundleEncoder<TBackend> handle,
        string markerLabel);

    internal void PopDebugGroup(
        GPURenderBundleEncoder<TBackend> handle);

    internal void PushDebugGroup(
        GPURenderBundleEncoder<TBackend> handle,
        string groupLabel);

    internal void SetBindGroup(
        GPURenderBundleEncoder<TBackend> handle,
        int index,
        GPUBindGroup<TBackend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsets);

    internal void SetBindGroup(
        GPURenderBundleEncoder<TBackend> handle,
        int index,
        GPUBindGroup<TBackend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsetsData,
        ulong dynamicOffsetsDataStart,
        uint dynamicOffsetsDataLength);

    internal void SetIndexBuffer(
        GPURenderBundleEncoder<TBackend> handle,
        GPUBuffer<TBackend> buffer,
        GPUIndexFormat indexFormat,
        ulong offset,
        ulong size);

    internal void SetPipeline(
        GPURenderBundleEncoder<TBackend> handle,
        GPURenderPipeline<TBackend> pipeline);

    internal void SetVertexBuffer(
        GPURenderBundleEncoder<TBackend> handle,
        int slot,
        GPUBuffer<TBackend>? buffer,
        ulong offset,
        ulong size);

    #endregion

    #region GPURenderPassEncoder methods

    internal void BeginOcclusionQuery(
        GPURenderPassEncoder<TBackend> handle,
        uint queryIndex);

    internal void Draw(
        GPURenderPassEncoder<TBackend> handle,
        uint vertexCount,
        uint instanceCount,
        uint firstVertex,
        uint firstInstance);

    internal void DrawIndexed(
        GPURenderPassEncoder<TBackend> handle,
        uint indexCount,
        uint instanceCount,
        uint firstIndex,
        int baseVertex,
        uint firstInstance);

    internal void DrawIndexedIndirect(
        GPURenderPassEncoder<TBackend> handle,
        GPUBuffer<TBackend> indirectBuffer,
        ulong indirectOffset);

    internal void DrawIndirect(
        GPURenderPassEncoder<TBackend> handle,
        GPUBuffer<TBackend> indirectBuffer,
        ulong indirectOffset);

    internal void End(
        GPURenderPassEncoder<TBackend> handle);

    internal void EndOcclusionQuery(
        GPURenderPassEncoder<TBackend> handle);

    internal void ExecuteBundles(
        GPURenderPassEncoder<TBackend> handle,
        ReadOnlySpan<GPURenderBundle<TBackend>> bundles);

    internal void InsertDebugMarker(
        GPURenderPassEncoder<TBackend> handle,
        string markerLabel);

    internal void PopDebugGroup(
        GPURenderPassEncoder<TBackend> handle);

    internal void PushDebugGroup(
        GPURenderPassEncoder<TBackend> handle,
        string groupLabel);

    internal void SetBindGroup(
        GPURenderPassEncoder<TBackend> handle,
        int index,
        GPUBindGroup<TBackend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsets);

    internal void SetBindGroup(
        GPURenderPassEncoder<TBackend> handle,
        int index,
        GPUBindGroup<TBackend>? bindGroup,
        ReadOnlySpan<uint> dynamicOffsetsData,
        ulong dynamicOffsetsDataStart,
        uint dynamicOffsetsDataLength);

    internal void SetBlendConstant(
        GPURenderPassEncoder<TBackend> handle,
        GPUColor color);

    internal void SetIndexBuffer(
        GPURenderPassEncoder<TBackend> handle,
        GPUBuffer<TBackend> buffer,
        GPUIndexFormat indexFormat,
        ulong offset,
        ulong size);

    internal void SetPipeline(
        GPURenderPassEncoder<TBackend> handle,
        GPURenderPipeline<TBackend> pipeline);

    internal void SetScissorRect(
        GPURenderPassEncoder<TBackend> handle,
        uint x,
        uint y,
        uint width,
        uint height);

    internal void SetStencilReference(
        GPURenderPassEncoder<TBackend> handle,
        uint reference);

    internal void SetVertexBuffer(
        GPURenderPassEncoder<TBackend> handle,
        int slot,
        GPUBuffer<TBackend>? buffer,
        ulong offset,
        ulong size);

    internal void SetViewport(
        GPURenderPassEncoder<TBackend> handle,
        float x,
        float y,
        float width,
        float height,
        float minDepth,
        float maxDepth);

    #endregion

    #region GPURenderPipeline methods

    internal GPUBindGroupLayout<TBackend> GetBindGroupLayout(
        GPURenderPipeline<TBackend> handle,
        ulong index);

    #endregion

    #region GPUShaderModule methods

    internal ValueTask<GPUCompilationInfo> GetCompilationInfoAsync(
        GPUShaderModule<TBackend> handle,
        CancellationToken cancellation);

    #endregion

    #region GPUSurface methods

    internal void Configure(
        GPUSurface<TBackend> handle,
        GPUSurfaceConfiguration configuration);

    internal GPUTexture<TBackend> GetCurrentTexture(
        GPUSurface<TBackend> handle);

    internal void Unconfigure(
        GPUSurface<TBackend> handle);

    #endregion

    #region GPUTexture methods

    internal GPUTextureView<TBackend> CreateView(
        GPUTexture<TBackend> handle,
        GPUTextureViewDescriptor descriptor);

    #endregion

}

