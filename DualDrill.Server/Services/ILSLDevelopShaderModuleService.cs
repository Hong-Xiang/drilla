using DualDrill.Engine.Shader;
using DualDrill.Graphics;
using DualDrill.ILSL;
namespace DualDrill.Server.Services;

public sealed class ILSLDevelopShaderModuleService
{
    public Dictionary<string, IILSLDevelopShaderModule> ShaderModules { get; } = new()
    {
        [nameof(MinimumTriangle)] = new MinimumTriangle(),
        //[nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(VertexOutputShader)] = new VertexOutputShader(),
        //[nameof(SimpleUniformShader)] = new SimpleUniformShader(),
        
    };

    public Dictionary<string, IShaderModule> DemoShaderModules { get; } = new()
    {
        [nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(SimpleUniformShader)] = new SimpleUniformShader(),
        [nameof(QuadShader)] = new QuadShader(),

    };
}
