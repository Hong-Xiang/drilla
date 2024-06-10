using DualDrill.Graphics.WebGPU.Native;
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
        WGPU.wgpuRenderPassEncoderSetPipeline(Handle, pipeline);
    }
    public unsafe void SetPipeline(GPURenderPipeline pipeline)
    {
        WGPU.wgpuRenderPassEncoderSetPipeline(Handle, pipeline.Handle);
    }
    public unsafe void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
    {
        WGPU.wgpuRenderPassEncoderDraw(Handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    public unsafe void End()
    {
        WGPU.wgpuRenderPassEncoderEnd(Handle);
    }
}

