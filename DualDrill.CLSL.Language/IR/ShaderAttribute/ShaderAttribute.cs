namespace DualDrill.CLSL.Language.IR.ShaderAttribute;

public interface IShaderAttribute : IShaderAstNode { }
public interface IShaderStageAttribute : IShaderAttribute { }

public sealed class ShaderMethodAttribute() : Attribute, IShaderStageAttribute
{
}

public sealed class UniformAttribute() : Attribute, IShaderAttribute { }
public sealed class ReadAttribute() : Attribute, IShaderAttribute { }
public sealed class ReadWriteAttribute() : Attribute, IShaderAttribute { }
