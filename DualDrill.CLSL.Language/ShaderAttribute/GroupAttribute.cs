namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class GroupAttribute(int Binding) : Attribute, IShaderAttribute
{
    public int Binding { get; } = Binding;

    public override string ToString()
        => $"@group({Binding})";
}
