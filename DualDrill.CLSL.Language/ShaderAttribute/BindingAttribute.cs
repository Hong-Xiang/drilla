namespace DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;

public sealed class BindingAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
}

