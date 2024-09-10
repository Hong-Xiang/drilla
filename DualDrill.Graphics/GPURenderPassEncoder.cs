using DualDrill.Graphics.Interop;

namespace DualDrill.Graphics;

public partial interface IGPURenderPassEncoder : IDisposable
{
    string Label { get; }
    public void BeginOcclusionQuery(uint queryIndex);
    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);
    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0);
    public void DrawIndexedIndirect(IGPUBuffer indirectBuffer, ulong indirectOffset);
    public void DrawIndirect(IGPUBuffer indirectBuffer, ulong indirectOffset);
    public void End();
    public void EndOcclusionQuery();
    public void ExecuteBundles(IEnumerable<IGPURenderBundle> bundles);
    public void InsertDebugMarker(string markerLabel);
    public void PopDebugGroup();
    public void PushDebugGroup(string groupLabel);
    public void SetBindGroup(int index, IGPUBindGroup? bindGroup, ReadOnlySpan<uint> dynamicOffsets = default);
    public void SetBlendConstant(GPUColor color);
    public void SetIndexBuffer(IGPUBuffer buffer, GPUIndexFormat indexFormat, ulong offset, ulong size);
    public void SetPipeline(IGPURenderPipeline pipeline);
    public void SetScissorRect(uint x, uint y, uint width, uint height);
    public void SetStencilReference(uint reference);
    public void SetVertexBuffer(int slot, IGPUBuffer? buffer, ulong offset = 0, ulong? size = default);
    public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth);
}

public sealed partial record class GPURenderPassEncoder<TBackend>(GPUHandle<TBackend, GPURenderPassEncoder<TBackend>> Handle)
    : IDisposable, IGPURenderPassEncoder
    where TBackend : IBackend<TBackend>
{
    public string Label { get; init; } = string.Empty;

    public void BeginOcclusionQuery(uint queryIndex)
    {
        TBackend.Instance.BeginOcclusionQuery(this, queryIndex);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        TBackend.Instance.Draw(this, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance)
    {
        TBackend.Instance.DrawIndexed(this, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }

    public void DrawIndexedIndirect(IGPUBuffer indirectBuffer, ulong indirectOffset)
    {
        TBackend.Instance.DrawIndexedIndirect(this, (GPUBuffer<TBackend>)indirectBuffer, indirectOffset);
    }

    public void DrawIndirect(IGPUBuffer indirectBuffer, ulong indirectOffset)
    {
        TBackend.Instance.DrawIndirect(this, (GPUBuffer<TBackend>)indirectBuffer, indirectOffset);
    }

    public void End()
    {
        TBackend.Instance.End(this);
    }

    public void EndOcclusionQuery()
    {
        TBackend.Instance.EndOcclusionQuery(this);
    }

    public void ExecuteBundles(IEnumerable<IGPURenderBundle> bundles)
    {
        throw new NotImplementedException();
        //TBackend.Instance.ExecuteBundles(this, bundles);
    }

    public void InsertDebugMarker(string markerLabel)
    {
        TBackend.Instance.InsertDebugMarker(this, markerLabel);
    }

    public void PopDebugGroup()
    {
        TBackend.Instance.PopDebugGroup(this);
    }

    public void PushDebugGroup(string groupLabel)
    {
        TBackend.Instance.PushDebugGroup(this, groupLabel);
    }

    public void SetBindGroup(int index, IGPUBindGroup? bindGroup, ReadOnlySpan<uint> dynamicOffsets)
    {
        TBackend.Instance.SetBindGroup(this, index, (GPUBindGroup<TBackend>?)bindGroup, dynamicOffsets);
    }

    public void SetBindGroup(int index, IGPUBindGroup? bindGroup, ReadOnlySpan<uint> dynamicOffsetsData, ulong dynamicOffsetsDataStart, uint dynamicOffsetsDataLength)
    {
        TBackend.Instance.SetBindGroup(this, index, (GPUBindGroup<TBackend>?)bindGroup, dynamicOffsetsData, dynamicOffsetsDataStart, dynamicOffsetsDataLength);
    }

    public void SetBlendConstant(GPUColor color)
    {
        TBackend.Instance.SetBlendConstant(this, color);
    }

    public void SetIndexBuffer(IGPUBuffer buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        TBackend.Instance.SetIndexBuffer(this, (GPUBuffer<TBackend>)buffer, indexFormat, offset, size);
    }

    public void SetPipeline(IGPURenderPipeline pipeline)
    {
        TBackend.Instance.SetPipeline(this, (GPURenderPipeline<TBackend>)pipeline);
    }

    public void SetScissorRect(uint x, uint y, uint width, uint height)
    {
        TBackend.Instance.SetScissorRect(this, x, y, width, height);
    }

    public void SetStencilReference(uint reference)
    {
        TBackend.Instance.SetStencilReference(this, reference);
    }

    public void SetVertexBuffer(int slot, IGPUBuffer? buffer, ulong offset, ulong? size)
    {
        TBackend.Instance.SetVertexBuffer(this, slot, (GPUBuffer<TBackend>?)buffer, offset, size ?? buffer?.Length ?? throw new GraphicsApiException<TBackend>("Buffer size unknown"));
    }

    public void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
    {
        TBackend.Instance.SetViewport(this, x, y, width, height, minDepth, maxDepth);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}

public sealed partial class GPURenderPassEncoder
{
    public unsafe void SetPipeline(WGPURenderPipelineImpl* pipeline)
    {
        WGPU.RenderPassEncoderSetPipeline(Handle, pipeline);
    }
    public unsafe void SetPipeline(GPURenderPipeline pipeline)
    {
        WGPU.RenderPassEncoderSetPipeline(Handle, pipeline.Handle);
    }
    public unsafe void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        WGPU.RenderPassEncoderDraw(Handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }
    public unsafe void SetBindGroup(int index, GPUBindGroup group)
    {
        WGPU.RenderPassEncoderSetBindGroup(Handle, (uint)index, group.Handle, 0, null);
    }

    public unsafe void SetVertexBuffer(int index, GPUBuffer buffer, ulong offset, ulong size)
    {
        WGPU.RenderPassEncoderSetVertexBuffer(Handle, (uint)index, buffer.Handle, offset, size);
    }
    public unsafe void SetIndexBuffer(GPUBuffer buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        WGPU.RenderPassEncoderSetIndexBuffer(Handle, buffer.Handle, (GPUIndexFormat)indexFormat, offset, size);
    }

    public unsafe void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
    {
        WGPU.RenderPassEncoderDrawIndexed(Handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }
    public unsafe void End()
    {
        WGPU.RenderPassEncoderEnd(Handle);
    }
}

