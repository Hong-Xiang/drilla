namespace DualDrill.Graphics;

public partial interface IBackend<TBackend>
    : IDisposable
       where TBackend : IBackend<TBackend>
{
    public abstract static TBackend Instance { get; }
    internal ValueTask<GPUDevice> RequestDeviceAsyncLegacy(
        GPUAdapter<TBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    );
    //createTexture(descriptor: GPUTextureDescriptor): GPUTexture
    //createSampler(descriptor: GPUSamplerDescriptor): GPUSampler
    //importExternalTexture(descriptor: GPUExternalTextureDescriptor): GPUExternalTexture
    //createBindGroupLayout(descriptor: GPUBindGroupLayoutDescriptor): GPUBindGroupLayout
    //createPipelineLayout(descriptor: GPUPipelineLayoutDescriptor): GPUPipelineLayout
    //createBindGroup(descriptor: GPUBindGroupDescriptor): GPUBindGroup
    //createShaderModule(descriptor: GPUShaderModuleDescriptor): GPUShaderModule
    //createComputePipeline(descriptor: GPUComputePipelineDescriptor): GPUComputePipeline
    //createRenderPipeline(descriptor: GPURenderPipelineDescriptor): GPURenderPipeline
    //createComputePipelineAsync(descriptor: GPUComputePipelineDescriptor): Promise<GPUComputePipeline>
    //createRenderPipelineAsync(descriptor: GPURenderPipelineDescriptor): Promise<GPURenderPipeline>
    //createCommandEncoder(descriptor: GPUCommandEncoderDescriptor): GPUCommandEncoder
    //createRenderBundleEncoder(descriptor: GPURenderBundleEncoderDescriptor): GPURenderBundleEncoder
    //createQuerySet(descriptor: GPUQuerySetDescriptor): GPUQuerySet

    internal GPUTextureView<TBackend> CreateTextureView(GPUTexture<TBackend> texture, GPUTextureViewDescriptor descriptor);

    // GPUInstance
    //ValueTask<GPUAdaptor<TBackend>> RequestAdapterAsync(GPUInstance<TBackend> instance, GPURequestAdapterOptions options, CancellationToken cancellation);
    //GPUTextureFormat GetPreferredCanvasFormat(GPUInstance<TBackend> instance);

    // GPUAdapter

    // GPUSurface/GPUCanvasContext
    //void Present(GPUSurface<TBackend> surface);
}

