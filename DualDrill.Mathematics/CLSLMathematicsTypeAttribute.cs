namespace DualDrill.Mathematics;

internal sealed class CLSLMathematicsTypeAttribute(string Name) : Attribute
{
    public string Name { get; } = Name;
}
