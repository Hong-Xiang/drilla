using DualDrill.Graphics;
using DualDrill.Graphics.Backend;
using Silk.NET.Vulkan;

namespace DualDrill.Engine.Renderer;

public class StaticTriangleRenderer : IRenderer<StaticTriangleRenderer.State>
{
    public readonly record struct State()
    {
    }

    readonly GPUDevice Device;
    GPUShaderModule ShaderModule { get; }
    GPURenderPipeline Pipeline { get; }

    static readonly string SHADER_CODE = @"
      @vertex fn vs(
        @builtin(vertex_index) vertexIndex : u32
      ) -> @builtin(position) vec4f {
        var pos = array(
          vec2f( 0.0,  0.5),  // top center
          vec2f(-0.5, -0.5),  // bottom left
          vec2f( 0.5, -0.5)   // bottom right
        );
 
        return vec4f(pos[vertexIndex], 0.0, 1.0);
      }
 
      @fragment fn fs() -> @location(0) vec4f {
        return vec4f(1.0, 1.0, 0.5, 1.0);
      }
";

    public readonly GPUTextureFormat TextureFormat = GPUTextureFormat.BGRA8UnormSrgb;


    public StaticTriangleRenderer(GPUDevice device)
    {
        Device = device;
        ShaderModule = Device.CreateShaderModule(SHADER_CODE);
        Pipeline = GPURenderPipeline.Create(Device, new GPURenderPipelineDescriptor()
        {
            Vertex = new GPUVertexState()
            {
                Module = ShaderModule,
                EntryPoint = "vs"
            },
            Fragment = new GPUFragmentState()
            {
                Module = ShaderModule,
                EntryPoint = "fs",
                Targets = new GPUColorTargetState[]
                {
                    new()
                    {
                        Format = TextureFormat,
                        WriteMask = (uint)GPUColorWriteMask.All
                    }
                }
            },
            Primitive = new()
            {
                Topology = GPUPrimitiveTopology.TriangleList
            }
        });
    }

    public void Render(double time, GPUQueue queue, GPUTexture texture, State data)
    {
        using var view = texture.CreateView();
        using var encoder = Device.CreateCommandEncoder(new());

        var pass = encoder.BeginRenderPass(new GPURenderPassDescriptor()
        {
            ColorAttachments = new GPURenderPassColorAttachment[]
            {
                new()
                {
                    View = view,
                    LoadOp = GPULoadOp.Load,
                    StoreOp = GPUStoreOp.Store,
                }
            }
        });

        pass.SetPipeline(Pipeline);
        pass.Draw(3, 1, 0, 0);
        pass.End();

        using var commands = encoder.Finish(new());
        queue.Submit([commands]);
    }
}

