namespace DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;

public sealed class GroupAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
}
