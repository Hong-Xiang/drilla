using DualDrill.Graphics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DualDrill.Engine.Renderer;



public sealed class WebGPULogoRenderer :
    IRenderer<WebGPULogoRenderer.State>,
    IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct State(
        Vector2 Position,
        Vector2 Scale
    )
    {
    }

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

    private readonly WebGPULogoMesh Model = new();

    public readonly GPUTextureFormat TextureFormat = GPUTextureFormat.RGBA8UnormSrgb;

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

struct UniformData {
    position: vec2f,
    scale: vec2f
}

@group(0) @binding(0) var<uniform> uData: UniformData;

@vertex
fn vs_main(@location(0) position: vec2f,
           @location(1) color: vec3f) 
-> VertexOutput {
    //let ratio = 640.0 / 480.0; 
    //var offset = vec2f(-0.6875, -0.463);
    //offset += 0.3 * vec2f(cos(uTime), sin(uTime));
    // let p = position + offset;
    let p = position * uData.scale  + uData.position;
    var out : VertexOutput;
    out.position =  vec4<f32>(p, 0.0, 1.0);
    out.color = color;
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return vec4<f32>(in.color, 1.0);
}";

    public WebGPULogoRenderer(GPUDevice device)
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
                                    MinBindingSize = 4 * sizeof(float)
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
            Size = sizeof(float) * 4,
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
                    Size = sizeof(float) * 4
                }
            ]
        });


        var queue = Device.GetQueue();
        queue.WriteBuffer(VertexBuffer, 0, Model.VertexData);
        queue.WriteBuffer(IndexBuffer, 0, Model.IndexData);
        //ReadOnlySpan<float> time = [1.0f];
        queue.WriteBuffer(UniformBuffer, 0, [0.0f, 0.0f, 640f / 480f, 1.0f]);
    }

    public void Render(double time, GPUQueue queue, GPUTexture renderTarget, State state)
    {
        using var view = renderTarget.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());
        var offset = new Vector2(-0.6875f, -0.463f);
        var position = state.Position + offset;
        //var scale = new Vector2(480f / 640f, 1.0f);
        //var offset = new Vector2(-0.6875f, -0.463f) + 0.3f * new Vector2(MathF.Cos((float)time), MathF.Sin((float)time));

        //let ratio = 640.0 / 480.0; 
        //var offset = vec2f(-0.6875, -0.463);
        //offset += 0.3 * vec2f(cos(uTime), sin(uTime));

        //queue.WriteBuffer(UniformBuffer, 0, [(float)time / 10]);
        queue.WriteBuffer(UniformBuffer, 0, [position.X, position.Y, state.Scale.X, state.Scale.Y]);

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
