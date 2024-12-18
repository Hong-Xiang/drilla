using DualDrill.Graphics;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class FragmentAttribute() : Attribute, IShaderStageAttribute
{
    public GPUShaderStage Stage => GPUShaderStage.Fragment;
}
