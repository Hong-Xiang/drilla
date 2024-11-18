namespace DualDrill.CLSL.Language.IR.ShaderAttribute;

public sealed class GroupAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
}
