using DualDrill.Graphics;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class ComputeAttribute : Attribute, IShaderStageAttribute
{
    public GPUShaderStage Stage => GPUShaderStage.Compute;
}