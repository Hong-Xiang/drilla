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
    internal ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(
        GPUAdapter<TBackend> handle,
        GPUDeviceDescriptor descriptor,
        CancellationToken cancellationToken);

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
        CancellationToken cancellationToken);

    internal ValueTask<GPURenderPipeline<TBackend>> CreateRenderPipelineAsyncAsync(
        GPUDevice<TBackend> handle,
        GPURenderPipelineDescriptor descriptor,
        CancellationToken cancellationToken);

    internal GPUCommandEncoder<TBackend> CreateCommandEncoder(
        GPUDevice<TBackend> handle,
        GPUCommandEncoderDescriptor descriptor);

    internal GPURenderBundleEncoder<TBackend> CreateRenderBundleEncoder(
        GPUDevice<TBackend> handle,
        GPURenderBundleEncoderDescriptor descriptor);

    internal GPUQuerySet<TBackend> CreateQuerySet(
        GPUDevice<TBackend> handle,
        GPUQuerySetDescriptor descriptor);

    internal ValueTask<GPUAdapter<TBackend>?> RequestAdapterAsync(
        GPUInstance<TBackend> handle,
        GPURequestAdapterOptions options,
        CancellationToken cancellationToken);

    internal GPUTextureFormat GetPreferredCanvasFormat(
        GPUInstance<TBackend> handle);
}