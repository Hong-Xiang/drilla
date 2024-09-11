namespace DualDrill.Graphics;
public partial interface IGPURenderBundle
{
}

public sealed partial record class GPURenderBundle<TBackend>(GPUHandle<TBackend, GPURenderBundle<TBackend>> Handle)
    : IDisposable, IGPURenderBundle
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUPipelineLayout : IDisposable
{
}

public sealed partial record class GPUPipelineLayout<TBackend>(GPUHandle<TBackend, GPUPipelineLayout<TBackend>> Handle)
    : IDisposable, IGPUPipelineLayout
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUComputePipeline
{
}

public sealed partial record class GPUComputePipeline<TBackend>(GPUHandle<TBackend, GPUComputePipeline<TBackend>> Handle)
    : IDisposable, IGPUComputePipeline
    where TBackend : IBackend<TBackend>
{

    public GPUBindGroupLayout<TBackend> GetBindGroupLayout(
     ulong index
    )
    {
        return TBackend.Instance.GetBindGroupLayout(this, index);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUShaderModule : IDisposable
{
}

public sealed partial record class GPUShaderModule<TBackend>(GPUHandle<TBackend, GPUShaderModule<TBackend>> Handle)
    : IDisposable, IGPUShaderModule
    where TBackend : IBackend<TBackend>
{

    public ValueTask<GPUCompilationInfo> GetCompilationInfoAsync(
     CancellationToken cancellation
    )
    {
        return TBackend.Instance.GetCompilationInfoAsync(this, cancellation);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUQuerySet
{
}

public sealed partial record class GPUQuerySet<TBackend>(GPUHandle<TBackend, GPUQuerySet<TBackend>> Handle)
    : IDisposable, IGPUQuerySet
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUSampler
{
}

public sealed partial record class GPUSampler<TBackend>(GPUHandle<TBackend, GPUSampler<TBackend>> Handle)
    : IDisposable, IGPUSampler
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUBindGroup
{
}

public sealed partial record class GPUBindGroup<TBackend>(GPUHandle<TBackend, GPUBindGroup<TBackend>> Handle)
    : IDisposable, IGPUBindGroup
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPURenderPipeline : IDisposable
{
    public IGPUBindGroupLayout GetBindGroupLayout(ulong index);
}

public sealed partial record class GPURenderPipeline<TBackend>(GPUHandle<TBackend, GPURenderPipeline<TBackend>> Handle)
    : IDisposable, IGPURenderPipeline
    where TBackend : IBackend<TBackend>
{

    public IGPUBindGroupLayout GetBindGroupLayout(ulong index)
    {
        return TBackend.Instance.GetBindGroupLayout(this, index);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUCommandBuffer : IDisposable
{
}

public sealed partial record class GPUCommandBuffer<TBackend>(GPUHandle<TBackend, GPUCommandBuffer<TBackend>> Handle)
    : IDisposable, IGPUCommandBuffer
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUBindGroupLayout
{
}

public sealed partial record class GPUBindGroupLayout<TBackend>(GPUHandle<TBackend, GPUBindGroupLayout<TBackend>> Handle)
    : IDisposable, IGPUBindGroupLayout
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPUComputePassEncoder
{
}

public sealed partial record class GPUComputePassEncoder<TBackend>(GPUHandle<TBackend, GPUComputePassEncoder<TBackend>> Handle)
    : IDisposable, IGPUComputePassEncoder
    where TBackend : IBackend<TBackend>
{

    public void DispatchWorkgroups(
     uint workgroupCountX
    , uint workgroupCountY
    , uint workgroupCountZ
    )
    {
        TBackend.Instance.DispatchWorkgroups(this, workgroupCountX, workgroupCountY, workgroupCountZ);
    }

    public void DispatchWorkgroupsIndirect(
     GPUBuffer<TBackend> indirectBuffer
    , ulong indirectOffset
    )
    {
        TBackend.Instance.DispatchWorkgroupsIndirect(this, indirectBuffer, indirectOffset);
    }

    public void End(
    )
    {
        TBackend.Instance.End(this);
    }

    public void InsertDebugMarker(
     string markerLabel
    )
    {
        TBackend.Instance.InsertDebugMarker(this, markerLabel);
    }

    public void PopDebugGroup(
    )
    {
        TBackend.Instance.PopDebugGroup(this);
    }

    public void PushDebugGroup(
     string groupLabel
    )
    {
        TBackend.Instance.PushDebugGroup(this, groupLabel);
    }

    public void SetBindGroup(
     int index
    , GPUBindGroup<TBackend>? bindGroup
    , ReadOnlySpan<uint> dynamicOffsets
    )
    {
        TBackend.Instance.SetBindGroup(this, index, bindGroup, dynamicOffsets);
    }

    public void SetBindGroup(
     int index
    , GPUBindGroup<TBackend>? bindGroup
    , ReadOnlySpan<uint> dynamicOffsetsData
    , ulong dynamicOffsetsDataStart
    , uint dynamicOffsetsDataLength
    )
    {
        TBackend.Instance.SetBindGroup(this, index, bindGroup, dynamicOffsetsData, dynamicOffsetsDataStart, dynamicOffsetsDataLength);
    }

    public void SetPipeline(
     GPUComputePipeline<TBackend> pipeline
    )
    {
        TBackend.Instance.SetPipeline(this, pipeline);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public partial interface IGPURenderBundleEncoder
{
}

public sealed partial record class GPURenderBundleEncoder<TBackend>(GPUHandle<TBackend, GPURenderBundleEncoder<TBackend>> Handle)
    : IDisposable, IGPURenderBundleEncoder
    where TBackend : IBackend<TBackend>
{

    public void Draw(
     uint vertexCount
    , uint instanceCount
    , uint firstVertex
    , uint firstInstance
    )
    {
        TBackend.Instance.Draw(this, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndexed(
     uint indexCount
    , uint instanceCount
    , uint firstIndex
    , int baseVertex
    , uint firstInstance
    )
    {
        TBackend.Instance.DrawIndexed(this, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }

    public void DrawIndexedIndirect(
     GPUBuffer<TBackend> indirectBuffer
    , ulong indirectOffset
    )
    {
        TBackend.Instance.DrawIndexedIndirect(this, indirectBuffer, indirectOffset);
    }

    public void DrawIndirect(
     GPUBuffer<TBackend> indirectBuffer
    , ulong indirectOffset
    )
    {
        TBackend.Instance.DrawIndirect(this, indirectBuffer, indirectOffset);
    }

    public GPURenderBundle<TBackend> Finish(
     GPURenderBundleDescriptor descriptor
    )
    {
        return TBackend.Instance.Finish(this, descriptor);
    }

    public void InsertDebugMarker(
     string markerLabel
    )
    {
        TBackend.Instance.InsertDebugMarker(this, markerLabel);
    }

    public void PopDebugGroup(
    )
    {
        TBackend.Instance.PopDebugGroup(this);
    }

    public void PushDebugGroup(
     string groupLabel
    )
    {
        TBackend.Instance.PushDebugGroup(this, groupLabel);
    }

    public void SetBindGroup(
     int index
    , GPUBindGroup<TBackend>? bindGroup
    , ReadOnlySpan<uint> dynamicOffsets
    )
    {
        TBackend.Instance.SetBindGroup(this, index, bindGroup, dynamicOffsets);
    }

    public void SetBindGroup(
     int index
    , GPUBindGroup<TBackend>? bindGroup
    , ReadOnlySpan<uint> dynamicOffsetsData
    , ulong dynamicOffsetsDataStart
    , uint dynamicOffsetsDataLength
    )
    {
        TBackend.Instance.SetBindGroup(this, index, bindGroup, dynamicOffsetsData, dynamicOffsetsDataStart, dynamicOffsetsDataLength);
    }

    public void SetIndexBuffer(
     GPUBuffer<TBackend> buffer
    , GPUIndexFormat indexFormat
    , ulong offset
    , ulong size
    )
    {
        TBackend.Instance.SetIndexBuffer(this, buffer, indexFormat, offset, size);
    }

    public void SetPipeline(
     GPURenderPipeline<TBackend> pipeline
    )
    {
        TBackend.Instance.SetPipeline(this, pipeline);
    }

    public void SetVertexBuffer(
     int slot
    , GPUBuffer<TBackend>? buffer
    , ulong offset
    , ulong size
    )
    {
        TBackend.Instance.SetVertexBuffer(this, slot, buffer, offset, size);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
