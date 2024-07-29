using DualDrill.Graphics;
using Silk.NET.WebGPU;
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
    private GPUBuffer UniformBuffer { get; }
    private GPUBindGroupLayout UniformBindGroupLayout { get; }
    private GPUBindGroup UniformBindGroup { get; }

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

@group(0) @binding(0) var<uniform> uTime: f32;

@vertex
fn vs_main(@location(0) position: vec2f,
           @location(1) color: vec3f) 
-> VertexOutput {
    let ratio = 640.0 / 480.0; 
    var offset = vec2f(-0.6875, -0.463);
    offset += 0.3 * vec2f(cos(uTime), sin(uTime));
    // let p = position + offset;
    let p = vec2f(position.x, position.y * ratio) + offset;
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
        UniformBindGroupLayout = Device.CreateBindGroupLayout(
                    new()
                    {
                        Entries = (GPUBindGroupLayoutEntry[])[
                            new() {
                                Binding = 0,
                                Visibility = GPUShaderStage.Vertex,
                                Buffer = new(){
                                    Type = GPUBufferBindingType.Uniform,
                                    MinBindingSize = sizeof(float)
                                }
                            }
                        ]
                    }
        );
        PipelineLayout = Device.CreatePipelineLayout(new GPUPipelineLayoutDescriptor()
        {
            BindGroupLayouts = (GPUBindGroupLayout[])[
                UniformBindGroupLayout
            ]
        });

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
        UniformBuffer = Device.CreateBuffer(new()
        {
            Size = sizeof(float),
            Usage = GPUBufferUsage.CopyDst | GPUBufferUsage.Uniform
        });
        UniformBindGroup = Device.CreateBindGroup(new()
        {
            Layout = UniformBindGroupLayout,
            Entries = (GPUBindGroupEntry[])[
                new()
                {
                    Binding = 0,
                    Buffer = UniformBuffer,
                    Offset = 0,
                    Size = sizeof(float)
                }
            ]
        });


        var queue = Device.GetQueue();
        queue.WriteBuffer(VertexBuffer, 0, Model.VertexData);
        queue.WriteBuffer(IndexBuffer, 0, Model.IndexData);
        ReadOnlySpan<float> time = [1.0f];
        queue.WriteBuffer(UniformBuffer, 0, time);
    }

    public async ValueTask RenderAsync(double time, GPUQueue queue, GPUTexture renderTarget)
    {
        using var view = renderTarget.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());

        queue.WriteBuffer(UniformBuffer, 0, [(float)time / 10]);

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
        rp.SetBindGroup(0, UniformBindGroup);
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
