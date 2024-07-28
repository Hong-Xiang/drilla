using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUCommandEncoder
{
    public unsafe GPURenderPassEncoder BeginRenderPass(GPURenderPassDescriptor descriptor)
    {
        WGPURenderPassDepthStencilAttachment depthStencilAttachment = default;
        if (descriptor.DepthStencilAttachment.HasValue)
        {
            throw new NotImplementedException();
        }
        var colorAttachments = stackalloc WGPURenderPassColorAttachment[descriptor.ColorAttachments.Length];
        for (var i = 0; i < descriptor.ColorAttachments.Length; i++)
        {
            var c = descriptor.ColorAttachments.Span[i];
            colorAttachments[i] = new WGPURenderPassColorAttachment
            {
                view = c.View.Handle,
                loadOp = (GPULoadOp)c.LoadOp,
                storeOp = (GPUStoreOp)c.StoreOp,
                clearValue =
                {
                    r = c.ClearValue.R,
                    g = c.ClearValue.G,
                    b = c.ClearValue.B,
                    a = c.ClearValue.A
                }
            };
            if (c.ResolveTarget is not null)
            {
                colorAttachments[i].resolveTarget = c.ResolveTarget.Handle;
            }
        }
        WGPURenderPassDescriptor nativeDescriptor = new WGPURenderPassDescriptor
        {
            colorAttachmentCount = (uint)descriptor.ColorAttachments.Length,
            colorAttachments = colorAttachments,
            depthStencilAttachment = descriptor.DepthStencilAttachment.HasValue ? &depthStencilAttachment : null
        };
        return new(WGPU.CommandEncoderBeginRenderPass(Handle, &nativeDescriptor));
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
        WGPUImageCopyTexture nativeSource = new()
        {
            texture = source.Texture.Handle,
            aspect = (GPUTextureAspect)source.Aspect,
            mipLevel = (uint)source.MipLevel
        };
        WGPUImageCopyBuffer nativeDestination = new()
        {
            buffer = destination.Buffer.Handle,
            layout =
            {
                bytesPerRow = (uint)destination.Layout.BytesPerRow,
                offset = (uint)destination.Layout.Offset,
                rowsPerImage = (uint)destination.Layout.RowsPerImage
            }
        };
        WGPUExtent3D nativeCopySize = new()
        {
            width = (uint)copySize.Width,
            height = (uint)copySize.Height,
            depthOrArrayLayers = (uint)copySize.DepthOrArrayLayers,
        };
        WGPU.CommandEncoderCopyTextureToBuffer(Handle, &nativeSource, &nativeDestination, &nativeCopySize);
    }
}
