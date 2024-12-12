namespace DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;

public sealed class LocationAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
}
