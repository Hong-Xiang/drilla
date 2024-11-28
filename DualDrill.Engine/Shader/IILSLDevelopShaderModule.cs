using DualDrill.ILSL;

namespace DualDrill.Engine.Shader;

public interface IILSLDevelopShaderModule : ISharpShader
{
    public string ILSLWGSLExpectedCode { get; }
}
