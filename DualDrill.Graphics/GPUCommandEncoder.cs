using DualDrill.Graphics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;


public partial interface IGPUCommandEncoder : IDisposable
{
    public IGPUComputePassEncoder BeginComputePass(GPUComputePassDescriptor descriptor);
    public IGPURenderPassEncoder BeginRenderPass(GPURenderPassDescriptor descriptor);
    public void ClearBuffer(IGPUBuffer buffer, ulong offset, ulong size);
    public void CopyBufferToBuffer(IGPUBuffer source, ulong sourceOffset, IGPUBuffer destination, ulong destinationOffset, ulong size);
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



public sealed partial class GPUCommandEncoder
{
    public unsafe GPURenderPassEncoder BeginRenderPass(GPURenderPassDescriptor descriptor)
    {
        //WGPURenderPassDepthStencilAttachment depthStencilAttachment = default;
        //if (descriptor.DepthStencilAttachment.HasValue)
        //{
        //    throw new NotImplementedException();
        //}
        //var colorAttachments = stackalloc WGPURenderPassColorAttachment[descriptor.ColorAttachments.Length];
        //for (var i = 0; i < descriptor.ColorAttachments.Length; i++)
        //{
        //    var c = descriptor.ColorAttachments.Span[i];
        //    colorAttachments[i] = new WGPURenderPassColorAttachment
        //    {
        //        //view = c.View.Handle,
        //        loadOp = (GPULoadOp)c.LoadOp,
        //        storeOp = (GPUStoreOp)c.StoreOp,
        //        clearValue =
        //        {
        //            r = c.ClearValue.R,
        //            g = c.ClearValue.G,
        //            b = c.ClearValue.B,
        //            a = c.ClearValue.A
        //        }
        //    };
        //    if (c.ResolveTarget is not null)
        //    {
        //        colorAttachments[i].resolveTarget = c.ResolveTarget.Handle;
        //    }
        //}
        //WGPURenderPassDescriptor nativeDescriptor = new WGPURenderPassDescriptor
        //{
        //    colorAttachmentCount = (uint)descriptor.ColorAttachments.Length,
        //    colorAttachments = colorAttachments,
        //    depthStencilAttachment = descriptor.DepthStencilAttachment.HasValue ? &depthStencilAttachment : null
        //};
        //return new(WGPU.CommandEncoderBeginRenderPass(Handle, &nativeDescriptor));
        throw new NotImplementedException();
    }

    public unsafe GPUCommandBuffer Finish(GPUCommandBufferDescriptor descriptor)
    {
        WGPUCommandBufferDescriptor native = default;
        return new(WGPU.CommandEncoderFinish(Handle, &native));
    }

    public unsafe void CopyTextureToBuffer(
        GPUImageCopyTexture source,
        GPUImageCopyBuffer destination,
        GPUExtent3D copySize
    )
    {
        //WGPUImageCopyTexture nativeSource = new()
        //{
        //    texture = source.Texture.Handle,
        //    aspect = (GPUTextureAspect)source.Aspect,
        //    mipLevel = (uint)source.MipLevel
        //};
        //WGPUImageCopyBuffer nativeDestination = new()
        //{
        //    buffer = destination.Buffer.Handle,
        //    layout =
        //    {
        //        bytesPerRow = (uint)destination.Layout.BytesPerRow,
        //        offset = (uint)destination.Layout.Offset,
        //        rowsPerImage = (uint)destination.Layout.RowsPerImage
        //    }
        //};
        //WGPUExtent3D nativeCopySize = new()
        //{
        //    width = (uint)copySize.Width,
        //    height = (uint)copySize.Height,
        //    depthOrArrayLayers = (uint)copySize.DepthOrArrayLayers,
        //};
        //WGPU.CommandEncoderCopyTextureToBuffer(Handle, &nativeSource, &nativeDestination, &nativeCopySize);
        throw new NotImplementedException();
    }
}
