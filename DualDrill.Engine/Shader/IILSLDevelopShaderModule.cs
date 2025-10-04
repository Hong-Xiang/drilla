using DualDrill.CLSL;

namespace DualDrill.Engine.Shader;

public interface IILSLDevelopShaderModule : ISharpShader
{
    public string ILSLWGSLExpectedCode { get; }
}

public sealed class CLSLDevelopExpectedWGPUCodeAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}
