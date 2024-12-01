using DualDrill.Engine.Shader;
using DualDrill.ILSL;
namespace DualDrill.Server.Services;

public sealed class ILSLDevelopShaderModuleService
{
    public Dictionary<string, ISharpShader> ShaderModules { get; } = new()
    {
        [nameof(MinimumTriangle)] = new MinimumTriangle(),
        //[nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(GradientColorTriangleShader)] = new GradientColorTriangleShader(),
        //[nameof(SimpleUniformShader)] = new SimpleUniformShader(),
        [nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(SimpleUniformShader)] = new SimpleUniformShader(),
        [nameof(QuadShader)] = new QuadShader(),
    };
}
