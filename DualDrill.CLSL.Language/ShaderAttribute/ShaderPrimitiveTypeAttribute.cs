using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public interface IShaderPrimitiveTypeAttribute : IZeroArgumentNewLikeShaderMetadataAttribute
{
    IShaderType ShaderType { get; }
}

public sealed class ShaderPrimitiveTypeAttribute<TShaderType>
    : Attribute
    , IShaderPrimitiveTypeAttribute
    where TShaderType : ISingletonShaderType<TShaderType>
{
    public IShaderType ShaderType => TShaderType.Instance;
}
