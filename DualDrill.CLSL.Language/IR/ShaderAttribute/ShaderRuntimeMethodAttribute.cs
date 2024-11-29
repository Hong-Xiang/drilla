namespace DualDrill.CLSL.Language.IR.ShaderAttribute;

public sealed class ShaderRuntimeMethodAttribute : Attribute, IShaderMetadataAttribute
{
}

public sealed class VecPositionalValueConstructorMethodAttribute : Attribute, IShaderMetadataAttribute { }
public sealed class VecBroadcastConstructorMethodAttribute : Attribute, IShaderMetadataAttribute { }
public sealed class VecConversionConstructorMethodAttribute : Attribute, IShaderMetadataAttribute { }
public sealed class VecZeroConstructorMethodAttribute : Attribute, IShaderMetadataAttribute { }
