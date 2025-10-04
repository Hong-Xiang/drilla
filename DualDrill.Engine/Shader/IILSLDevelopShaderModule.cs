using DualDrill.ILSL;

namespace DualDrill.Engine.Shader;

public interface IILSLDevelopShaderModule : IShaderModule
{
    public string ILSLWGSLExpectedCode { get; }
}
