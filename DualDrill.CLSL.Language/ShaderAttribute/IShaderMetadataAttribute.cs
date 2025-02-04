using DualDrill.Common;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public interface IShaderMetadataAttribute : IShaderAttribute
{
    string GetCSharpUsageCode();
}

public interface IZeroArgumentNewLikeShaderMetadataAttribute : IShaderMetadataAttribute
{
    string IShaderMetadataAttribute.GetCSharpUsageCode() => GetType().CSharpFullName();
}
