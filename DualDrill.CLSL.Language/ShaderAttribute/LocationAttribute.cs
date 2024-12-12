namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class LocationAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
}
