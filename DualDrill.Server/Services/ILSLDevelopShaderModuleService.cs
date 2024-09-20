using DualDrill.Engine.Shader;

namespace DualDrill.Server.Services;

public sealed class ILSLDevelopShaderModuleService
{
    public Dictionary<string, IILSLDevelopShaderModule> ShaderModules { get; } = new()
    {
        [nameof(MinimumTriangle)] = new MinimumTriangle(),
        [nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(VertexOutputShader)] = new VertexOutputShader(),
    };
}
