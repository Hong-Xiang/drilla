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
    private GPUBuffer IndexBuffer { get; }
    private float[] VertexData = [
    -0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
    +0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
    +0.5f, +0.5f, 0.0f, 0.0f, 1.0f,
    -0.5f, +0.5f, 1.0f, 1.0f, 0.0f
    ];
    private UInt16[] IndexData = [0, 1, 2, 0, 2, 3];

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
    private const string SHADER = @"

struct VertexOutput {
    @builtin(position) position: vec4f,
    @location(0)       color: vec3f,
}

@vertex
fn vs_main(@location(0) position: vec2f,
           @location(1) color: vec3f) 
-> VertexOutput {
    var out : VertexOutput;
    out.position =  vec4<f32>(position, 0.0, 1.0);
    out.color = color;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.color, 1.0);
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
                        ArrayStride = 5 * sizeof(float),
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = new []
                        {
                            new GPUVertexAttribute
                            {
                                ShaderLocation = 0,
                                Format = GPUVertexFormat.Float32x2,
                                Offset = 0
                            },
                            new GPUVertexAttribute
                            {
                                ShaderLocation = 1,
                                Format = GPUVertexFormat.Float32x3,
                                Offset = 2 * sizeof(float),
                            },
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
        IndexBuffer = Device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = (ulong)IndexData.Length * sizeof(UInt16),
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.Index
        });
        var queue = Device.GetQueue();
        queue.WriteBuffer<float>(VertexBuffer, 0, VertexData);
        queue.WriteBuffer<ushort>(IndexBuffer, 0, IndexData);
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
        rp.SetIndexBuffer(IndexBuffer, GPUIndexFormat.Uint16, 0, (ulong)IndexData.Length * sizeof(UInt16));
        //rp.Draw(6, 1, 0, 0);
        rp.DrawIndexed((uint)IndexData.Length);
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
