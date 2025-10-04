using DualDrill.Graphics;

namespace DualDrill.Engine.Renderer;

public class StaticTriangleRenderer : IRenderer<StaticTriangleRenderer.State>
{
    public readonly record struct State()
    {
    }

    readonly IGPUDevice Device;
    IGPUShaderModule ShaderModule { get; }
    IGPURenderPipeline Pipeline { get; }

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


    public StaticTriangleRenderer(IGPUDevice device)
    {
        Device = device;
        ShaderModule = Device.CreateShaderModule(new() { Code = SHADER_CODE });
        Pipeline = Device.CreateRenderPipeline(new GPURenderPipelineDescriptor()
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
                        WriteMask = GPUColorWriteMask.All
                    }
                }
            },
            Primitive = new()
            {
                Topology = GPUPrimitiveTopology.TriangleList
            }
        });
    }

    public void Render(double time, IGPUQueue queue, IGPUTexture texture, State data)
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

