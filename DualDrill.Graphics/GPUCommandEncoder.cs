namespace DualDrill.Graphics;


public partial interface IGPUCommandEncoder : IDisposable
{
    public IGPUComputePassEncoder BeginComputePass(GPUComputePassDescriptor descriptor);
    public IGPURenderPassEncoder BeginRenderPass(GPURenderPassDescriptor descriptor);
    public void ClearBuffer(IGPUBuffer buffer, ulong offset, ulong size);
    public void CopyBufferToBuffer(IGPUBuffer source, ulong sourceOffset, IGPUBuffer destination, ulong destinationOffset, ulong size);
    public void CopyTextureToBuffer(GPUImageCopyTexture source, GPUImageCopyBuffer destination, GPUExtent3D copySize);
    public unsafe IGPUCommandBuffer Finish(GPUCommandBufferDescriptor descriptor);
}

public sealed partial record class GPUCommandEncoder<TBackend>(GPUHandle<TBackend, GPUCommandEncoder<TBackend>> Handle)
    : IDisposable, IGPUCommandEncoder
    where TBackend : IBackend<TBackend>
{

    public IGPUComputePassEncoder BeginComputePass(GPUComputePassDescriptor descriptor)
    {
        return TBackend.Instance.BeginComputePass(this, descriptor);
    }

    public IGPURenderPassEncoder BeginRenderPass(GPURenderPassDescriptor descriptor)
    {
        return TBackend.Instance.BeginRenderPass(this, descriptor);
    }

    public void ClearBuffer(IGPUBuffer buffer, ulong offset, ulong size)
    {
        TBackend.Instance.ClearBuffer(this, (GPUBuffer<TBackend>)buffer, offset, size);
    }

    public void CopyBufferToBuffer(IGPUBuffer source, ulong sourceOffset, IGPUBuffer destination, ulong destinationOffset, ulong size)
    {
        TBackend.Instance.CopyBufferToBuffer(this, (GPUBuffer<TBackend>)source, sourceOffset, (GPUBuffer<TBackend>)destination, destinationOffset, size);
    }

    public void CopyBufferToTexture(GPUImageCopyBuffer source, GPUImageCopyTexture destination, GPUExtent3D copySize)
    {
        TBackend.Instance.CopyBufferToTexture(this, source, destination, copySize);
    }

    public void CopyTextureToBuffer(GPUImageCopyTexture source, GPUImageCopyBuffer destination, GPUExtent3D copySize)
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

    public IGPUCommandBuffer Finish(GPUCommandBufferDescriptor descriptor)
    {
        return TBackend.Instance.Finish(this, descriptor);
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

    public void ResolveQuerySet(GPUQuerySet<TBackend> querySet, uint firstQuery, uint queryCount, GPUBuffer<TBackend> destination, ulong destinationOffset)
    {
        TBackend.Instance.ResolveQuerySet(this, querySet, firstQuery, queryCount, destination, destinationOffset);
    }

    public void Dispose()
    {
        TBackend.Instance.DisposeHandle(Handle);
    }
}


