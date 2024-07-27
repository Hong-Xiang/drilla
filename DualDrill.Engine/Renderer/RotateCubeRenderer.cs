using DualDrill.Graphics;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Engine.Renderer;

public sealed class RotateCubeRenderer
{
    readonly GPUDevice Device;
    GPUShaderModule ShaderModule { get; }
    GPUPipelineLayout PipelineLayout { get; }
    GPURenderPipeline Pipeline { get; }
    GPUBuffer VertexBuffer { get; }
    GPUBindGroupLayout BindGroupLayout { get; }
    GPUBindGroup BindGroup { get; }

    public readonly GPUTextureFormat TextureFormat = GPUTextureFormat.BGRA8UnormSrgb;

    private const string SHADER = @"
struct Uniforms {
  modelViewProjectionMatrix : mat4x4f,
}
@binding(0) @group(0) var<uniform> uniforms : Uniforms;

struct VertexOutput {
  @builtin(position) Position : vec4f,
  @location(0) fragUV : vec2f,
  @location(1) fragPosition: vec4f,
}

@vertex
fn vs_main(
  @location(0) position : vec4f,
  @location(1) uv : vec2f
) -> VertexOutput {
  var output : VertexOutput;
  output.Position = uniforms.modelViewProjectionMatrix * position;
  output.fragUV = uv;
  output.fragPosition = 0.5 * (position + vec4(1.0, 1.0, 1.0, 1.0));
  return output;
}

@fragment
fn fs_main(
  @location(0) fragUV: vec2f,
  @location(1) fragPosition: vec4f
) -> @location(0) vec4f {
  return fragPosition;
}";

    static readonly float[] CubeVertexArray = [
  // float4 position, float4 color, float2 uv,
  1, -1, 1, 1,   1, 0, 1, 1,  0, 1,
  -1, -1, 1, 1,  0, 0, 1, 1,  1, 1,
  -1, -1, -1, 1, 0, 0, 0, 1,  1, 0,
  1, -1, -1, 1,  1, 0, 0, 1,  0, 0,
  1, -1, 1, 1,   1, 0, 1, 1,  0, 1,
  -1, -1, -1, 1, 0, 0, 0, 1,  1, 0,

  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,
  1, -1, 1, 1,   1, 0, 1, 1,  1, 1,
  1, -1, -1, 1,  1, 0, 0, 1,  1, 0,
  1, 1, -1, 1,   1, 1, 0, 1,  0, 0,
  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,
  1, -1, -1, 1,  1, 0, 0, 1,  1, 0,

  -1, 1, 1, 1,   0, 1, 1, 1,  0, 1,
  1, 1, 1, 1,    1, 1, 1, 1,  1, 1,
  1, 1, -1, 1,   1, 1, 0, 1,  1, 0,
  -1, 1, -1, 1,  0, 1, 0, 1,  0, 0,
  -1, 1, 1, 1,   0, 1, 1, 1,  0, 1,
  1, 1, -1, 1,   1, 1, 0, 1,  1, 0,

  -1, -1, 1, 1,  0, 0, 1, 1,  0, 1,
  -1, 1, 1, 1,   0, 1, 1, 1,  1, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0,
  -1, -1, -1, 1, 0, 0, 0, 1,  0, 0,
  -1, -1, 1, 1,  0, 0, 1, 1,  0, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0,

  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,
  -1, 1, 1, 1,   0, 1, 1, 1,  1, 1,
  -1, -1, 1, 1,  0, 0, 1, 1,  1, 0,
  -1, -1, 1, 1,  0, 0, 1, 1,  1, 0,
  1, -1, 1, 1,   1, 0, 1, 1,  0, 0,
  1, 1, 1, 1,    1, 1, 1, 1,  0, 1,

  1, -1, -1, 1,  1, 0, 0, 1,  0, 1,
  -1, -1, -1, 1, 0, 0, 0, 1,  1, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0,
  1, 1, -1, 1,   1, 1, 0, 1,  0, 0,
  1, -1, -1, 1,  1, 0, 0, 1,  0, 1,
  -1, 1, -1, 1,  0, 1, 0, 1,  1, 0 ];


    public RotateCubeRenderer(
        GPUDevice device
    )
    {
        Device = device;
        ShaderModule = Device.CreateShaderModule(SHADER);
        //BindGroupLayout = Device.CreateBindGroupLayout(new GPUBindGroupLayoutDescriptor
        //{
        //    Entries = new[]
        //    {
        //        new GPUBindGroupLayoutEntry ()
        //        {
        //            Binding = 0,
        //            Buffer = new GPUBufferBindingLayout()
        //            {
        //            }
        //        }
        //    }
        //});
        //PipelineLayout = Device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor()
        //{
        //    BindGroupLayouts = new GPUBindGroupLayout[]
        //    {
        //        BindGroupLayout
        //    }
        //});
        Pipeline = GPURenderPipeline.Create(Device, new GPURenderPipelineDescriptor()
        {
            Vertex = new GPUVertexState
            {
                Module = ShaderModule,
                EntryPoint = "vs_main",
                Buffers = (GPUVertexBufferLayout[])[
                    new GPUVertexBufferLayout {
                        ArrayStride = 4 * 10,
                        Attributes = (GPUVertexAttribute[])[
                            new GPUVertexAttribute {
                                ShaderLocation = 0,
                                Offset = 0,
                                Format = GPUVertexFormat.Float32x4
                            },
                            new GPUVertexAttribute {
                                ShaderLocation = 1,
                                Offset = 4 * 8,
                                Format = GPUVertexFormat.Float32x2
                            }
                        ]
                    }
                ]
            },
            Primitive = new GPUPrimitiveState
            {
                Topology = GPUPrimitiveTopology.TriangleList,
                CullMode = GPUCullMode.Back
            },
            Multisample = new GPUMultisampleState
            {
                Count = 1,
                Mask = ~0u
            },
            DepthStencil = new GPUDepthStencilState
            {
                DepthWriteEnabled = true,
                DepthCompare = GPUCompareFunction.Less,
                Format = GPUTextureFormat.Depth24Plus
            },
            Fragment = new GPUFragmentState
            {
                Module = ShaderModule,
                EntryPoint = "fs_main",
                Targets = new[]{new GPUColorTargetState {
                        Format = TextureFormat,
                        WriteMask = (uint)GPUColorWriteMask.All
                    }}
            },
            Layout = PipelineLayout
        });

        VertexBuffer = Device.CreateBuffer(new GPUBufferDescriptor
        {
            MappedAtCreation = true,
            Size = VertexBufferByteSize,
            Usage = GPUBufferUsage.Uniform
        });

        var cpuData = CubeVertexArray.AsSpan();
        var gpuBuffer = VertexBuffer.GetMappedRange(0, (int)VertexBufferByteSize);
        var cpuByteData = MemoryMarshal.Cast<float, byte>(cpuData);
        cpuByteData.CopyTo(gpuBuffer);

        //BindGroup = Device.CreateBindGroup(new GPUBindGroupDescriptor
        //{
        //    Layout = Pipeline.GetBindGroupLayout(0),
        //    Entries = new GPUBindGroupEntry[]
        //    {
        //        new() {
        //        Binding = 0,
        //        Buffer = VertexBuffer
        //        }
        //    }
        //});
    }

    static readonly ulong VertexBufferByteSize = (ulong)(CubeVertexArray.Length * sizeof(float));

    static readonly ReadOnlyMemory<byte> Name = "abc"u8.ToArray();

    public async ValueTask RenderAsync(double time, GPUQueue queue, GPUTexture renderTarget)
    {
        using var view = renderTarget.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());

        using var rp = encoder.BeginRenderPass(new()
        {
            ColorAttachments = (GPURenderPassColorAttachment[])[
                new GPURenderPassColorAttachment() {
                    View = view,
                    LoadOp = GPULoadOp.Clear,
                    StoreOp = GPUStoreOp.Store,
                    ClearValue = new() {
                        R = 0.5,
                        G = 0.5,
                        B = 0.5,
                        A = 1
                    }
                }
            ]
        });

        rp.SetPipeline(Pipeline);
        //rp.SetBindGroup(0, BindGroup);
        rp.SetVertexBuffer(0, VertexBuffer, 0, VertexBufferByteSize);
        rp.Draw(3, 1, 0, 0);
        rp.End();

        using var drawCommands = encoder.Finish(new());
        queue.Submit([drawCommands]);
    }

    public void Dispose()
    {
        Pipeline.Dispose();
        PipelineLayout.Dispose();
        ShaderModule.Dispose();
    }

}
