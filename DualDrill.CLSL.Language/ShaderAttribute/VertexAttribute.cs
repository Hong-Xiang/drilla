using DualDrill.Graphics;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class VertexAttribute() : Attribute, IShaderStageAttribute
{
    public GPUShaderStage Stage => GPUShaderStage.Vertex;
}
