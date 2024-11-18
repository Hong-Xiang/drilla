namespace DualDrill.CLSL.Language.IR.ShaderAttribute;

public sealed class LocationAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
}
