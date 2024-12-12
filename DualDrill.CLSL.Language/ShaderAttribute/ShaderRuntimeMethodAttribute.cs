namespace DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;

public sealed class ShaderRuntimeMethodAttribute : Attribute, IShaderMetadataAttribute
{
}

public interface IVecConstructorMethodAttribute : IShaderMetadataAttribute { }

public sealed class VecPositionalValueConstructorMethodAttribute : Attribute, IVecConstructorMethodAttribute { }
public sealed class VecBroadcastConstructorMethodAttribute : Attribute, IVecConstructorMethodAttribute { }
public sealed class VecConversionConstructorMethodAttribute : Attribute, IVecConstructorMethodAttribute { }
public sealed class VecZeroConstructorMethodAttribute : Attribute, IVecConstructorMethodAttribute { }
