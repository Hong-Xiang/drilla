namespace DualDrill.CLSL.Language.ShaderAttribute;

public interface IShaderMetadataAttribute : IShaderAttribute
{
    string GetCSharpUsageCode();
}

public interface INoArgumentShaderMetadataAttribute : IShaderMetadataAttribute
{
    string IShaderMetadataAttribute.GetCSharpUsageCode()
    {
        return GetType().Name;
    }
}
