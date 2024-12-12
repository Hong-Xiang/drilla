namespace DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;

public sealed class AlignAttribute(int Value) : Attribute, IShaderAttribute
{
    public int Value { get; } = Value;
}
