using DualDrill.Engine.Shader;
using DualDrill.Graphics;
namespace DualDrill.Server.Services;

public sealed class ILSLDevelopShaderModuleService
{
    public Dictionary<string, IILSLDevelopShaderModule> ShaderModules { get; } = new()
    {
        [nameof(MinimumTriangle)] = new MinimumTriangle(),
        [nameof(VertexOutputShader)] = new VertexOutputShader(),
    };
}
