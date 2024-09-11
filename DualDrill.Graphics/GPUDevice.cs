namespace DualDrill.Graphics;

public partial interface IGPUDevice : IDisposable
{
    public IGPUQueue Queue { get; }

    public IGPUBindGroup CreateBindGroup(GPUBindGroupDescriptor descriptor);

    public IGPUBindGroupLayout CreateBindGroupLayout(
    GPUBindGroupLayoutDescriptor descriptor
    );

    public IGPUBuffer CreateBuffer(GPUBufferDescriptor descriptor);


    public IGPUCommandEncoder CreateCommandEncoder(GPUCommandEncoderDescriptor descriptor);


    public IGPUComputePipeline CreateComputePipeline(GPUComputePipelineDescriptor descriptor);


    public ValueTask<IGPUComputePipeline> CreateComputePipelineAsync(
    GPUComputePipelineDescriptor descriptor
    , CancellationToken cancellation
    );

    public IGPUPipelineLayout CreatePipelineLayout(GPUPipelineLayoutDescriptor descriptor);

    public IGPUQuerySet CreateQuerySet(GPUQuerySetDescriptor descriptor);
    public IGPURenderBundleEncoder CreateRenderBundleEncoder(GPURenderBundleEncoderDescriptor descriptor);
    public IGPURenderPipeline CreateRenderPipeline(GPURenderPipelineDescriptor descriptor);

    public ValueTask<IGPURenderPipeline> CreateRenderPipelineAsync(GPURenderPipelineDescriptor descriptor, CancellationToken cancellation);


    public IGPUSampler CreateSampler(GPUSamplerDescriptor descriptor);
    public IGPUShaderModule CreateShaderModule(GPUShaderModuleDescriptor descriptor);


    public IGPUTexture CreateTexture(GPUTextureDescriptor descriptor);

    public void Poll();

    public ValueTask PollAsync(CancellationToken cancellation);
}

public sealed partial record class GPUDevice<TBackend>(GPUHandle<TBackend, GPUDevice<TBackend>> Handle)
    : IDisposable, IGPUDevice
    where TBackend : IBackend<TBackend>
{
    public required IGPUQueue Queue { get; init; }

    public IGPUBindGroup CreateBindGroup(GPUBindGroupDescriptor descriptor)
    {
        return TBackend.Instance.CreateBindGroup(this, descriptor);
    }

    public IGPUBindGroupLayout CreateBindGroupLayout(GPUBindGroupLayoutDescriptor descriptor)
    {
        return TBackend.Instance.CreateBindGroupLayout(this, descriptor);
    }

    public IGPUBuffer CreateBuffer(GPUBufferDescriptor descriptor)
    {
        return TBackend.Instance.CreateBuffer(this, descriptor);
    }

    public IGPUCommandEncoder CreateCommandEncoder(GPUCommandEncoderDescriptor descriptor)
    {
        return TBackend.Instance.CreateCommandEncoder(this, descriptor);
    }

    public IGPUComputePipeline CreateComputePipeline(GPUComputePipelineDescriptor descriptor)
    {
        return TBackend.Instance.CreateComputePipeline(this, descriptor);
    }

    public async ValueTask<IGPUComputePipeline> CreateComputePipelineAsync(GPUComputePipelineDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.CreateComputePipelineAsync(this, descriptor, cancellation);
    }

    public IGPUPipelineLayout CreatePipelineLayout(GPUPipelineLayoutDescriptor descriptor)
    {
        return TBackend.Instance.CreatePipelineLayout(this, descriptor);
    }

    public IGPUQuerySet CreateQuerySet(GPUQuerySetDescriptor descriptor)
    {
        return TBackend.Instance.CreateQuerySet(this, descriptor);
    }

    public IGPURenderBundleEncoder CreateRenderBundleEncoder(GPURenderBundleEncoderDescriptor descriptor)
    {
        return TBackend.Instance.CreateRenderBundleEncoder(this, descriptor);
    }

    public IGPURenderPipeline CreateRenderPipeline(GPURenderPipelineDescriptor descriptor)
    {
        return TBackend.Instance.CreateRenderPipeline(this, descriptor);
    }

    public async ValueTask<IGPURenderPipeline> CreateRenderPipelineAsync(GPURenderPipelineDescriptor descriptor, CancellationToken cancellation)
    {
        return await TBackend.Instance.CreateRenderPipelineAsync(this, descriptor, cancellation);
    }

    public IGPUSampler CreateSampler(GPUSamplerDescriptor descriptor)
    {
        return TBackend.Instance.CreateSampler(this, descriptor);
    }

    public IGPUShaderModule CreateShaderModule(GPUShaderModuleDescriptor descriptor)
    {
        return TBackend.Instance.CreateShaderModule(this, descriptor);
    }

    public IGPUTexture CreateTexture(GPUTextureDescriptor descriptor)
    {
        return TBackend.Instance.CreateTexture(this, descriptor);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(((GPUQueue<TBackend>)Queue).Handle);
        TBackend.Instance.DisposeHandle(Handle);
    }

    public void Poll()
    {
        TBackend.Instance.Poll(this);
    }

    public ValueTask PollAsync(CancellationToken cancellation)
    {
        return TBackend.Instance.PollAsync(this, cancellation);
    }
}


