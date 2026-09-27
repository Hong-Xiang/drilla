namespace DualDrill.Graphics;

public partial interface IGPUComputePassEncoder : IDisposable
{
    void SetPipeline(IGPUComputePipeline pipeline);
    void SetBindGroup(int index, IGPUBindGroup? bindGroup, ReadOnlySpan<uint> dynamicOffsets = default);
    void DispatchWorkgroups(uint workgroupCountX, uint workgroupCountY = 1, uint workgroupCountZ = 1);
    void End();
}

public sealed partial record class GPUComputePassEncoder<TBackend>
    where TBackend : IBackend<TBackend>
{
    public void SetPipeline(IGPUComputePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline is not GPUComputePipeline<TBackend> typed)
            throw new ArgumentException("Pipeline belongs to another backend.", nameof(pipeline));
        TBackend.Instance.SetPipeline(this, typed);
    }

    public void SetBindGroup(int index, IGPUBindGroup? bindGroup, ReadOnlySpan<uint> dynamicOffsets = default)
    {
        if (bindGroup is not null and not GPUBindGroup<TBackend>)
            throw new ArgumentException("Bind group belongs to another backend.", nameof(bindGroup));
        TBackend.Instance.SetBindGroup(this, index, (GPUBindGroup<TBackend>?)bindGroup, dynamicOffsets);
    }
}
