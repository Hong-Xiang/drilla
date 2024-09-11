using DualDrill.Engine.Services;
using DualDrill.Graphics;

namespace DualDrill.Engine.Renderer;

public sealed class VolumeRenderer : IRenderer<VolumeRenderer.State>, IDisposable
{
    private IGPUDevice Device { get; }

    private static readonly string SHADER = """
        @group(0) @binding(0) var mySampler : sampler;
        @group(0) @binding(1) var myTexture : texture_3d<f32>;
        @group(0) @binding(2) var<uniform> depthValue : vec4f;

        struct VertexOutput {
          @builtin(position) Position : vec4f,
          @location(0) fragUV : vec2f,
        }

        const steps = 256;
       

        @vertex
        fn vert_main(@builtin(vertex_index) vertex_index : u32) -> VertexOutput {
          let scale = sqrt(3.0);
          var p : vec2f;
          if(vertex_index == 0u){ 
            p = vec2(1.0,  1.0);
          }
          if(vertex_index == 1u){ 
            p = vec2( 1.0, -1.0);
          }
          if(vertex_index == 2u){ 
            p = vec2(-1.0, -1.0);
          }
          if(vertex_index == 3u){ 
            p = vec2( 1.0,  1.0);
          }
          if(vertex_index == 4u){ 
            p = vec2(-1.0, -1.0);
          }
          if(vertex_index == 5u){ 
            p = vec2(-1.0,  1.0);
          }

          let pos = array(
            vec2( 1.0,  1.0),
            vec2( 1.0, -1.0),
            vec2(-1.0, -1.0),
            vec2( 1.0,  1.0),
            vec2(-1.0, -1.0),
            vec2(-1.0,  1.0),
          );

          var output : VertexOutput;
          output.Position = vec4(p, 0.0, 1.0);
          output.fragUV = vec2(p.x, - p.y) / 2 * scale;
          return output;
        }

        @fragment
        fn frag_main(@location(0) fragUV : vec2f) -> @location(0) vec4f {
          let scale = sqrt(3.0);
          let theta = depthValue.x;
          let phi = depthValue.y;
          let ru = vec3(
            sin(theta) * cos(phi),
            sin(theta) * sin(phi),
            cos(theta)
          );
          let thetau = vec3(
            cos(theta) * cos(phi),
            cos(theta) * sin(phi),
            -sin(theta)
          );
          let phiu = vec3(
            -sin(phi),
            cos(phi),
            0.0
          );
          let p = fragUV.x * thetau +  fragUV.y * phiu + depthValue.z * ru;
          var value = 0.0f;
          var window = depthValue.w;
          for(var i = 0; i < steps; i++){
            let uv = (p + scale / 2.0f) / scale + ru * window * (1.0f / f32(steps)) * f32(i - steps / 2);
            value +=  textureSample(myTexture, mySampler, uv).r;
          }
          value = value / f32(steps);

          return vec4(vec3(value), 1.0f);
        }
        """;


    private IGPUShaderModule ShaderModule { get; }
    private IGPUBindGroupLayout BindGroupLayout { get; }
    private IGPURenderPipeline Pipeline { get; }
    private IGPUBuffer UniformBuffer { get; }
    private IGPUBindGroup BindGroup { get; }
    private IGPUSampler Sampler { get; }
    private ITexture DataTexture { get; }

    private readonly int TextureWidth = 256;
    private readonly int TextureHeight = 256;
    private readonly int TextureDepth = 109;

    public VolumeRenderer(IGPUDevice device, TextureService textureService)
    {
        Device = device;
        ShaderModule = Device.CreateShaderModule(new() { Code = SHADER });
        Pipeline = Device.CreateRenderPipeline(new()
        {
            Vertex = new()
            {
                Module = ShaderModule,
                EntryPoint = "vert_main",
            },
            Fragment = new()
            {
                Module = ShaderModule,
                EntryPoint = "frag_main",
                Targets = new[] {
                    new GPUColorTargetState()
                    {
                        Format = GPUTextureFormat.BGRA8UnormSrgb
                    }
                }
            },
            Primitive = new()
            {
                Topology = GPUPrimitiveTopology.TriangleList
            }
        });
        BindGroupLayout = Pipeline.GetBindGroupLayout(0);
        DataTexture = textureService.GetTexture(Device, "head-volume");

        UniformBuffer = Device.CreateBuffer(new()
        {
            Size = 4 * sizeof(float),
            Usage = GPUBufferUsage.Uniform | GPUBufferUsage.CopyDst
        });
        var textureView = DataTexture.GPUTexture.CreateView();
        BindGroup = Device.CreateBindGroup(new GPUBindGroupDescriptor()
        {
            Label = "bind group 0 descriptor",
            Layout = BindGroupLayout,
            Entries = new GPUBindGroupEntry[]
            {
                new()
                {
                    Binding = 0,
                    Sampler = Sampler
                },
                new()
                {
                    Binding = 1,
                    TextureView = textureView
                },
                new()
                {
                    Binding = 2,
                    Buffer = UniformBuffer,
                    Offset = 0,
                    Size = 4 * sizeof(float)
                }
            }
        });
    }

    public void Dispose()
    {
        ShaderModule.Dispose();
    }

    public void Render(double time, IGPUQueue queue, IGPUTexture texture, State data)
    {
        queue.WriteBuffer(UniformBuffer, 0, [data.Theta, data.Phi, data.Z, data.Window]);
        using var encoder = Device.CreateCommandEncoder(new());
        var pass = encoder.BeginRenderPass(new()
        {
            ColorAttachments = new[]
            {
                new GPURenderPassColorAttachment()
                {
                    View = texture.CreateView(),
                    LoadOp = GPULoadOp.Clear,
                    StoreOp = GPUStoreOp.Store,
                }
            }
        });
        pass.SetPipeline(Pipeline);
        pass.SetBindGroup(0, BindGroup);
        pass.Draw(6);
        using var commands = encoder.Finish(new());
        queue.Submit([commands]);
    }

    public readonly record struct State(
        float Theta,
        float Phi,
        float Z,
        float Window
    )
    {
    }
}
