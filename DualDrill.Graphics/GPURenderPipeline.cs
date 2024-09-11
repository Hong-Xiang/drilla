using DualDrill.Graphics.Interop;
using Silk.NET.Core.Native;
using System.Reactive.Disposables;

namespace DualDrill.Graphics;

public sealed partial class GPURenderPipeline
{

    public unsafe GPUBindGroupLayout GetBindGroupLayout(int index)
    {
        return new GPUBindGroupLayout(WGPU.RenderPipelineGetBindGroupLayout(Handle, (uint)index));
    }

}
