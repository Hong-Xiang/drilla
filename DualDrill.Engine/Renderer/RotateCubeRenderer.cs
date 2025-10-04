using DualDrill.Engine.Scene;
using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using System.Runtime.InteropServices;

namespace DualDrill.Engine.Renderer;


public sealed class RotateCubeRenderer : IRenderer<RotateCubeRenderer.State>
{
    public readonly record struct State(
        Camera Camera,
        Cube Cube
    )
    {
    }

    readonly IGPUDevice Device;
    IGPUShaderModule ShaderModule { get; }
    IGPUPipelineLayout PipelineLayout { get; }
    IGPURenderPipeline Pipeline { get; }
    IGPUBuffer UniformBuffer { get; }
    IGPUBuffer VertexBuffer { get; }
    IGPUBindGroupLayout BindGroupLayout { get; }
    IGPUBindGroup BindGroup { get; }

    public readonly GPUTextureFormat TextureFormat = GPUTextureFormat.BGRA8UnormSrgb;

    private const string SHADER = @"
struct Uniforms {
  modelViewProjectionMatrix : mat4x4f,
}
@binding(0) @group(0) var<uniform> uniforms : Uniforms;

struct VertexOutput {
  @builtin(position) Position : vec4f,
  @location(0) uv : vec2f,
  @location(1) color: vec4f,
}

@vertex
fn vs_main(
  @location(0) position : vec4f,
  @location(1) color : vec4f,
  @location(2) uv : vec2f
) -> VertexOutput {
  var output : VertexOutput;
  output.Position = uniforms.modelViewProjectionMatrix * position;
  //output.Position = position;
  output.uv = uv;
  //output.color = 0.5 * (position + vec4(1.0, 1.0, 1.0, 1.0));
  output.color = color;
  return output;
}

@fragment
fn fs_main(
  @location(0) uv: vec2f,
  @location(1) color: vec4f
) -> @location(0) vec4f {
  return color;
  //return vec4f(1.0f, 1.0f, 0.0f, 1.0f);
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

    public int UniformBufferByteSize = 4 * 4 * 4;


    public RotateCubeRenderer(IGPUDevice device)
    {
        Device = device;
        ShaderModule = Device.CreateShaderModule(new()
        {
            Code = SHADER
        });
        BindGroupLayout = Device.CreateBindGroupLayout(new GPUBindGroupLayoutDescriptor
        {
            Entries = new[]
            {
                new GPUBindGroupLayoutEntry ()
                {
                    Binding = 0,
                    Visibility = GPUShaderStage.Vertex,
                    Buffer = new GPUBufferBindingLayout()
                    {
                        Type = GPUBufferBindingType.Uniform,
                        MinBindingSize = (ulong)UniformBufferByteSize
                    }
                }
            }
        });
        PipelineLayout = Device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor()
        {
            BindGroupLayouts = [BindGroupLayout]
        });
        Pipeline = Device.CreateRenderPipeline(new GPURenderPipelineDescriptor()
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
                                Offset = 4 * 4,
                                Format = GPUVertexFormat.Float32x4
                            },
                            new GPUVertexAttribute {
                                ShaderLocation = 2,
                                Offset = 4 * 8,
                                Format = GPUVertexFormat.Float32x2
                            }
                        ]
                    }
                ]
            },
            Primitive = new GPUPrimitiveState
            {
                FrontFace = GPUFrontFace.CCW,
                Topology = GPUPrimitiveTopology.TriangleList,
                CullMode = GPUCullMode.Back
            },
            Multisample = new GPUMultisampleState()
            {
                Count = 1,
                Mask = ~0u
            },
            //DepthStencil = new GPUDepthStencilState()
            //{
            //    DepthWriteEnabled = true,
            //    DepthCompare = GPUCompareFunction.Less,
            //    Format = GPUTextureFormat.Depth24Plus
            //},
            Fragment = new GPUFragmentState
            {
                Module = ShaderModule,
                EntryPoint = "fs_main",
                Targets = new[]{new GPUColorTargetState {
                    Format = TextureFormat,
                    WriteMask = GPUColorWriteMask.All
                }}
            },
            Layout = PipelineLayout
        });

        VertexBuffer = Device.CreateBuffer(new GPUBufferDescriptor
        {
            MappedAtCreation = true,
            Size = VertexBufferByteSize,
            Usage = GPUBufferUsage.Vertex
        });

        UniformBuffer = Device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = VertexBufferByteSize,
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst
        });

        for (var i = 0; i < CubeVertexArray.Length; i++)
        {
            if (i % 10 < 3)
            {
                CubeVertexArray[i] = CubeVertexArray[i] / 2.0f;
            }
        }

        var cpuData = CubeVertexArray.AsSpan();
        var gpuBuffer = VertexBuffer.GetMappedRange(0, (ulong)VertexBufferByteSize);
        var cpuByteData = MemoryMarshal.Cast<float, byte>(cpuData);
        cpuByteData.CopyTo(gpuBuffer);
        VertexBuffer.Unmap();

        BindGroup = Device.CreateBindGroup(new GPUBindGroupDescriptor
        {
            Layout = BindGroupLayout,
            Entries = new GPUBindGroupEntry[]
            {
                new() {
                Binding = 0,
                Buffer = UniformBuffer,
                Offset = 0,
                Size = (ulong) UniformBufferByteSize
                }
            }
        });
    }

    static readonly ulong VertexBufferByteSize = (ulong)(CubeVertexArray.Length * sizeof(float));

    static readonly ReadOnlyMemory<byte> Name = "abc"u8.ToArray();

    public unsafe void Render(double time, IGPUQueue queue, IGPUTexture renderTarget, State state)
    {
        using var view = renderTarget.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());

        var mvp = state.Cube.ModelMatrix * state.Camera.ViewProjectionMatrix;
        var mvpBuffer = new Span<float>(&mvp, 16);
        queue.WriteBuffer<float>(UniformBuffer, 0, mvpBuffer);

        using var rp = encoder.BeginRenderPass(new()
        {
            ColorAttachments = (GPURenderPassColorAttachment[])[
                new GPURenderPassColorAttachment() {
                    View = view,
                    LoadOp = GPULoadOp.Load,
                    StoreOp = GPUStoreOp.Store,
                }
            ]
        });

        rp.SetPipeline(Pipeline);
        rp.SetBindGroup(0, BindGroup);
        rp.SetVertexBuffer(0, VertexBuffer, 0, VertexBufferByteSize);
        rp.Draw(36, 1, 0, 0);
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
