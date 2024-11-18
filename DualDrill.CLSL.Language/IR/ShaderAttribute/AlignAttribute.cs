namespace DualDrill.CLSL.Language.IR.ShaderAttribute;

public sealed class AlignAttribute(int Value) : Attribute, IShaderAttribute
{
    public int Value { get; } = Value;
}
