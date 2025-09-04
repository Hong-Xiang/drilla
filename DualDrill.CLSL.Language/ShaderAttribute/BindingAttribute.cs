namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class BindingAttribute(int Binding, bool HasDynamicOffset = false) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;
    public bool HasDynamicOffset { get; } = HasDynamicOffset;

    public override string ToString()
        => HasDynamicOffset ? $"@binding({Binding}, has dynamic offset)" : $"@binding({Binding})";
}

