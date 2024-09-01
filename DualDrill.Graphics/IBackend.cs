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
    where TBackend : IBackend<TBackend>
{
    public abstract static TBackend Instance { get; }
    internal ValueTask<GPUAdapter<TBackend>> RequestAdapterAsync(
        GPUInstance<TBackend> instance,
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken);

    internal ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(
        GPUAdapter<TBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    );
    internal ValueTask<GPUDevice> RequestDeviceAsyncLegacy(
        GPUAdapter<TBackend> adapter,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellation
    );
    internal GPUBuffer<TBackend> CreateBuffer(GPUDevice<TBackend> device, GPUBufferDescriptor descriptor);
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

