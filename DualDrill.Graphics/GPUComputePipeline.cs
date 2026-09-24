namespace DualDrill.Graphics;

public partial interface IGPUComputePipeline
{
    IGPUBindGroupLayout GetBindGroupLayout(ulong index);
}

public sealed partial record class GPUComputePipeline<TBackend>
    where TBackend : IBackend<TBackend>
{
    IGPUBindGroupLayout IGPUComputePipeline.GetBindGroupLayout(ulong index) => GetBindGroupLayout(index);
}
