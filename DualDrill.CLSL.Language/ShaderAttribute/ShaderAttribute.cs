using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Graphics;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public interface IShaderAttribute : IShaderAstNode { }
public interface IAddressSpaceAttribute : IShaderAttribute
{
    IAddressSpace AddressSpace { get; }
}
public interface ISemanticBindingAttribute : IShaderAttribute { }
public interface IShaderStageAttribute : IShaderAttribute
{
    GPUShaderStage Stage { get; }
}

public sealed class ShaderMethodAttribute() : Attribute, IShaderStageAttribute
{
    public GPUShaderStage Stage => throw new NotImplementedException();
}
public sealed class ReadAttribute() : Attribute, IShaderAttribute { }
public sealed class ReadWriteAttribute() : Attribute, IShaderAttribute { }
