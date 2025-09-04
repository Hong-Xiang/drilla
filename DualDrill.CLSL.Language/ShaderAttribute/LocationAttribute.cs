namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class LocationAttribute(int Binding) : Attribute, ISemanticBindingAttribute
{
    public int Binding { get; } = Binding;
}
