using System.Collections.Immutable;

namespace DualDrill.Graphics;
public interface IGPUAdapter
{
}

public sealed partial record class GPUAdapter<TBackend>(GPUHandle<TBackend, GPUAdapter<TBackend>> Handle)
    : IDisposable, IGPUAdapter
    where TBackend : IBackend<TBackend>
{

    public ValueTask<GPUDevice<TBackend>> RequestDeviceAsync(
     GPUDeviceDescriptor descriptor
    , CancellationToken cancellation
    )
    {
        return TBackend.Instance.RequestDeviceAsync(this, descriptor, cancellation);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUBindGroup
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
public interface IGPUBindGroupLayout
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
public interface IGPUBuffer
{
}

public sealed partial record class GPUBuffer<TBackend>(GPUHandle<TBackend, GPUBuffer<TBackend>> Handle)
    : IDisposable, IGPUBuffer
    where TBackend : IBackend<TBackend>
{

    public ValueTask MapAsyncAsync(
     GPUMapMode mode
    , ulong offset
    , ulong size
    , CancellationToken cancellation
    )
    {
        return TBackend.Instance.MapAsyncAsync(this, mode, offset, size, cancellation);
    }

    public ReadOnlySpan<byte> GetMappedRange(
     ulong offset
    , ulong size
    )
    {
        return TBackend.Instance.GetMappedRange(this, offset, size);
    }

    public void Unmap(
    )
    {
        TBackend.Instance.Unmap(this);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUCommandBuffer
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
public interface IGPUCommandEncoder
{
}

public sealed partial record class GPUCommandEncoder<TBackend>(GPUHandle<TBackend, GPUCommandEncoder<TBackend>> Handle)
    : IDisposable, IGPUCommandEncoder
    where TBackend : IBackend<TBackend>
{

    public GPURenderPassEncoder<TBackend> BeginRenderPass(
     GPURenderPassDescriptor descriptor
    )
    {
        return TBackend.Instance.BeginRenderPass(this, descriptor);
    }

    public GPUComputePassEncoder<TBackend> BeginComputePass(
     GPUComputePassDescriptor descriptor
    )
    {
        return TBackend.Instance.BeginComputePass(this, descriptor);
    }

    public void CopyBufferToBuffer(
     GPUBuffer<TBackend> source
    , ulong sourceOffset
    , GPUBuffer<TBackend> destination
    , ulong destinationOffset
    , ulong size
    )
    {
        TBackend.Instance.CopyBufferToBuffer(this, source, sourceOffset, destination, destinationOffset, size);
    }

    public void CopyBufferToTexture(
     GPUImageCopyBuffer source
    , GPUImageCopyTexture destination
    , GPUExtent3D copySize
    )
    {
        TBackend.Instance.CopyBufferToTexture(this, source, destination, copySize);
    }

    public void CopyTextureToBuffer(
     GPUImageCopyTexture source
    , GPUImageCopyBuffer destination
    , GPUExtent3D copySize
    )
    {
        TBackend.Instance.CopyTextureToBuffer(this, source, destination, copySize);
    }

    public void CopyTextureToTexture(
     GPUImageCopyTexture source
    , GPUImageCopyTexture destination
    , GPUExtent3D copySize
    )
    {
        TBackend.Instance.CopyTextureToTexture(this, source, destination, copySize);
    }

    public void ClearBuffer(
     GPUBuffer<TBackend> buffer
    , ulong offset
    , ulong size
    )
    {
        TBackend.Instance.ClearBuffer(this, buffer, offset, size);
    }

    public void ResolveQuerySet(
     GPUQuerySet<TBackend> querySet
    , uint firstQuery
    , uint queryCount
    , GPUBuffer<TBackend> destination
    , ulong destinationOffset
    )
    {
        TBackend.Instance.ResolveQuerySet(this, querySet, firstQuery, queryCount, destination, destinationOffset);
    }

    public GPUCommandBuffer<TBackend> Finish(
     GPUCommandBufferDescriptor descriptor
    )
    {
        return TBackend.Instance.Finish(this, descriptor);
    }

    public void PushDebugGroup(
     string groupLabel
    )
    {
        TBackend.Instance.PushDebugGroup(this, groupLabel);
    }

    public void PopDebugGroup(
    )
    {
        TBackend.Instance.PopDebugGroup(this);
    }

    public void InsertDebugMarker(
     string markerLabel
    )
    {
        TBackend.Instance.InsertDebugMarker(this, markerLabel);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUComputePassEncoder
{
}

public sealed partial record class GPUComputePassEncoder<TBackend>(GPUHandle<TBackend, GPUComputePassEncoder<TBackend>> Handle)
    : IDisposable, IGPUComputePassEncoder
    where TBackend : IBackend<TBackend>
{

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

    public void PushDebugGroup(
     string groupLabel
    )
    {
        TBackend.Instance.PushDebugGroup(this, groupLabel);
    }

    public void PopDebugGroup(
    )
    {
        TBackend.Instance.PopDebugGroup(this);
    }

    public void InsertDebugMarker(
     string markerLabel
    )
    {
        TBackend.Instance.InsertDebugMarker(this, markerLabel);
    }

    public void SetPipeline(
     GPUComputePipeline<TBackend> pipeline
    )
    {
        TBackend.Instance.SetPipeline(this, pipeline);
    }

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

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUComputePipeline
{
}

public sealed partial record class GPUComputePipeline<TBackend>(GPUHandle<TBackend, GPUComputePipeline<TBackend>> Handle)
    : IDisposable, IGPUComputePipeline
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUDevice
{
}

public sealed partial record class GPUDevice<TBackend>(GPUHandle<TBackend, GPUDevice<TBackend>> Handle)
    : IDisposable, IGPUDevice
    where TBackend : IBackend<TBackend>
{

    public GPUBuffer<TBackend> CreateBuffer(
     GPUBufferDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateBuffer(this, descriptor);
    }

    public GPUTexture<TBackend> CreateTexture(
     GPUTextureDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateTexture(this, descriptor);
    }

    public GPUSampler<TBackend> CreateSampler(
     GPUSamplerDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateSampler(this, descriptor);
    }

    public GPUBindGroupLayout<TBackend> CreateBindGroupLayout(
     GPUBindGroupLayoutDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateBindGroupLayout(this, descriptor);
    }

    public GPUPipelineLayout<TBackend> CreatePipelineLayout(
     GPUPipelineLayoutDescriptor descriptor
    )
    {
        return TBackend.Instance.CreatePipelineLayout(this, descriptor);
    }

    public GPUBindGroup<TBackend> CreateBindGroup(
     GPUBindGroupDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateBindGroup(this, descriptor);
    }

    public GPUShaderModule<TBackend> CreateShaderModule(
     GPUShaderModuleDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateShaderModule(this, descriptor);
    }

    public GPUComputePipeline<TBackend> CreateComputePipeline(
     GPUComputePipelineDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateComputePipeline(this, descriptor);
    }

    public GPURenderPipeline<TBackend> CreateRenderPipeline(
     GPURenderPipelineDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateRenderPipeline(this, descriptor);
    }

    public ValueTask<GPUComputePipeline<TBackend>> CreateComputePipelineAsyncAsync(
     GPUComputePipelineDescriptor descriptor
    , CancellationToken cancellation
    )
    {
        return TBackend.Instance.CreateComputePipelineAsyncAsync(this, descriptor, cancellation);
    }

    public ValueTask<GPURenderPipeline<TBackend>> CreateRenderPipelineAsyncAsync(
     GPURenderPipelineDescriptor descriptor
    , CancellationToken cancellation
    )
    {
        return TBackend.Instance.CreateRenderPipelineAsyncAsync(this, descriptor, cancellation);
    }

    public GPUCommandEncoder<TBackend> CreateCommandEncoder(
     GPUCommandEncoderDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateCommandEncoder(this, descriptor);
    }

    public GPURenderBundleEncoder<TBackend> CreateRenderBundleEncoder(
     GPURenderBundleEncoderDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateRenderBundleEncoder(this, descriptor);
    }

    public GPUQuerySet<TBackend> CreateQuerySet(
     GPUQuerySetDescriptor descriptor
    )
    {
        return TBackend.Instance.CreateQuerySet(this, descriptor);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUInstance
{
}

public sealed partial record class GPUInstance<TBackend>(GPUHandle<TBackend, GPUInstance<TBackend>> Handle)
    : IDisposable, IGPUInstance
    where TBackend : IBackend<TBackend>
{

    public ValueTask<GPUAdapter<TBackend>?> RequestAdapterAsync(
     GPURequestAdapterOptions options
    , CancellationToken cancellation
    )
    {
        return TBackend.Instance.RequestAdapterAsync(this, options, cancellation);
    }

    public GPUTextureFormat GetPreferredCanvasFormat(
    )
    {
        return TBackend.Instance.GetPreferredCanvasFormat(this);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUPipelineLayout
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
public interface IGPUQuerySet
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
public interface IGPUQueue
{
}

public sealed partial record class GPUQueue<TBackend>(GPUHandle<TBackend, GPUQueue<TBackend>> Handle)
    : IDisposable, IGPUQueue
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderBundle
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
public interface IGPURenderBundleEncoder
{
}

public sealed partial record class GPURenderBundleEncoder<TBackend>(GPUHandle<TBackend, GPURenderBundleEncoder<TBackend>> Handle)
    : IDisposable, IGPURenderBundleEncoder
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderPassEncoder
{
}

public sealed partial record class GPURenderPassEncoder<TBackend>(GPUHandle<TBackend, GPURenderPassEncoder<TBackend>> Handle)
    : IDisposable, IGPURenderPassEncoder
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPURenderPipeline
{
}

public sealed partial record class GPURenderPipeline<TBackend>(GPUHandle<TBackend, GPURenderPipeline<TBackend>> Handle)
    : IDisposable, IGPURenderPipeline
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUSampler
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
public interface IGPUShaderModule
{
}

public sealed partial record class GPUShaderModule<TBackend>(GPUHandle<TBackend, GPUShaderModule<TBackend>> Handle)
    : IDisposable, IGPUShaderModule
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public sealed partial record class GPUSurface<TBackend>(GPUHandle<TBackend, GPUSurface<TBackend>> Handle)
    : IDisposable
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUTexture
{
}

public sealed partial record class GPUTexture<TBackend>(GPUHandle<TBackend, GPUTexture<TBackend>> Handle)
    : IDisposable, IGPUTexture
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}
public interface IGPUTextureView
{
}

public sealed partial record class GPUTextureView<TBackend>(GPUHandle<TBackend, GPUTextureView<TBackend>> Handle)
    : IDisposable, IGPUTextureView
    where TBackend : IBackend<TBackend>
{

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}

