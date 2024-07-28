using DualDrill.Graphics;
using System.Runtime.InteropServices;

namespace DualDrill.Engine.Renderer;

public sealed class SimpleColorRenderer : IDisposable
{
    readonly GPUDevice Device;

    //Graphics.ShaderModule Shader { get; set; }
    GPUShaderModule ShaderModule { get; set; }
    GPUPipelineLayout PipelineLayout { get; set; }
    GPURenderPipeline Pipeline { get; set; }
    private GPUBuffer VertexBuffer { get; }
    private GPUBuffer IndexBuffer { get; }

    private readonly WebGPULogo Model = new();

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
    let offset = vec2f(-0.6875, -0.463);
    let p = position + offset;
    var out : VertexOutput;
    out.position =  vec4<f32>(p, 0.0, 1.0);
    out.color = color;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.color, 1.0);
}";


    public SimpleColorRenderer(GPUDevice device)
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
                        ArrayStride = Model.ArrayStride,
                        StepMode = GPUVertexStepMode.Vertex,
                        Attributes = (GPUVertexAttribute[])[..Model.Attributes]
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
            Size = Model.VertexBufferByteLength,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.Vertex
        });
        IndexBuffer = Device.CreateBuffer(new GPUBufferDescriptor
        {
            Size = Model.IndexBufferByteLength,
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.Index
        });
        var queue = Device.GetQueue();
        queue.WriteBuffer(VertexBuffer, 0, Model.VertexData);
        queue.WriteBuffer(IndexBuffer, 0, Model.IndexData);
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
        rp.SetVertexBuffer(0, VertexBuffer, 0, Model.VertexBufferByteLength);
        rp.SetIndexBuffer(IndexBuffer, GPUIndexFormat.Uint16, 0, Model.IndexBufferByteLength);
        //rp.Draw(6, 1, 0, 0);
        rp.DrawIndexed((uint)Model.IndexCount);
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
