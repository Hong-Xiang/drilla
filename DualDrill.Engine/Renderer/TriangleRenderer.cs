using DualDrill.Graphics;
using System.Runtime.InteropServices;

namespace DualDrill.Engine.Renderer;

public sealed class TriangleRenderer : IDisposable
{
    readonly GPUDevice Device;

    //Graphics.ShaderModule Shader { get; set; }
    GPUShaderModule ShaderModule { get; set; }
    GPUPipelineLayout PipelineLayout { get; set; }
    GPURenderPipeline Pipeline { get; set; }
    private GPUBuffer VertexBuffer { get; }
    private float[] VertexData = [
         // x0, y0
    -0.5f, -0.5f,

    // x1, y1
    +0.5f, -0.5f,

    // x2, y2
    +0.0f, +0.5f,
       // Add a second triangle:
    -0.55f, -0.5f,
    -0.05f, +0.5f,
    -0.55f, +0.5f
    ];

    public readonly GPUTextureFormat TextureFormat = GPUTextureFormat.BGRA8UnormSrgb;

    private const string SHADER_OLD = @"@vertex
fn vs_main(@builtin(vertex_index) in_vertex_index: u32) -> @builtin(position) vec4<f32> {
    let x = f32(i32(in_vertex_index) - 1);
    let y = f32(i32(in_vertex_index & 1u) * 2 - 1);
    return vec4<f32>(x, y, 0.0, 1.0);
}

@fragment
fn fs_main() -> @location(0) vec4<f32> {
    return vec4<f32>(1.0, 1.0, 0.0, 1.0);
}";
    private const string SHADER = @"@vertex
fn vs_main(@location(0) in_vertex_position: vec2f) -> @builtin(position) vec4<f32> {
    return vec4<f32>(in_vertex_position, 0.0, 1.0);
}

@fragment
fn fs_main() -> @location(0) vec4<f32> {
    return vec4<f32>(1.0, 1.0, 0.0, 1.0);
}";


    public TriangleRenderer(GPUDevice device)
    {
        Device = device;
        ShaderModule = Device.CreateShaderModule(SHADER);
        PipelineLayout = Device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor());

        Pipeline = GPURenderPipeline.Create(Device, new GPURenderPipelineDescriptor()
        {
            Vertex = new GPUVertexState
            {
                Module = ShaderModule,
                EntryPoint = "vs_main",
                Buffers = new[] {
                    new GPUVertexBufferLayout
                    {
                        ArrayStride = 2 * sizeof(float),
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = new []
                        {
                            new GPUVertexAttribute
                            {
                                ShaderLocation = 0,
                                Format = GPUVertexFormat.Float32x2,
                                Offset = 0
                            }
                        }
                    }
                }
            },
            Primitive = new GPUPrimitiveState
            {
                Topology = GPUPrimitiveTopology.TriangleList
            },
            Multisample = new GPUMultisampleState
            {
                Count = 1,
                Mask = ~0u
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
            Size = (ulong)VertexData.Length * sizeof(float),
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.Vertex
        });
        Device.GetQueue().WriteBuffer(VertexBuffer, 0, MemoryMarshal.Cast<float, byte>(VertexData));
    }

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
                        R = (Math.Cos(time / 10.0f) + 1.0f) / 2,
                        G = (Math.Sin(time / 10.0f) + 1.0f) / 2,
                        B = 0,
                        A = 1
                    }
                }
            ]
        });

        rp.SetPipeline(Pipeline);
        rp.SetVertexBuffer(0, VertexBuffer, 0, (ulong)VertexData.Length * sizeof(float));
        rp.Draw(6, 1, 0, 0);
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
