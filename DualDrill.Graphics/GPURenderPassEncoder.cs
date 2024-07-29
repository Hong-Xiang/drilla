﻿using DualDrill.Graphics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;
public sealed partial class GPURenderPassEncoder
{
    public unsafe void SetPipeline(WGPURenderPipelineImpl* pipeline)
    {
        WGPU.RenderPassEncoderSetPipeline(Handle, pipeline);
    }
    public unsafe void SetPipeline(GPURenderPipeline pipeline)
    {
        WGPU.RenderPassEncoderSetPipeline(Handle, pipeline.Handle);
    }
    public unsafe void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
    {
        WGPU.RenderPassEncoderDraw(Handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }
    public unsafe void SetBindGroup(int index, GPUBindGroup group)
    {
        WGPU.RenderPassEncoderSetBindGroup(Handle, (uint)index, group.Handle, 0, null);
    }

    public unsafe void SetVertexBuffer(int index, GPUBuffer buffer, ulong offset, ulong size)
    {
        WGPU.RenderPassEncoderSetVertexBuffer(Handle, (uint)index, buffer.Handle, offset, size);
    }
    public unsafe void SetIndexBuffer(GPUBuffer buffer, GPUIndexFormat indexFormat, ulong offset, ulong size)
    {
        WGPU.RenderPassEncoderSetIndexBuffer(Handle, buffer.Handle, (GPUIndexFormat)indexFormat, offset, size);
    }

    public unsafe void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
    {
        WGPU.RenderPassEncoderDrawIndexed(Handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
    }
    public unsafe void End()
    {
        WGPU.RenderPassEncoderEnd(Handle);
    }
}

