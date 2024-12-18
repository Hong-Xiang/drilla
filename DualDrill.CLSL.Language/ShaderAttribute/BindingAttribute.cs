namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class BindingAttribute(int Binding, bool HasDynamicOffset = false) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
    public bool HasDynamicOffset { get; } = HasDynamicOffset;
}

