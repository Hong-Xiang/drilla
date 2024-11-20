namespace DualDrill.CLSL.CSharp;

public sealed class VecTypeProjectionAttribute(
    Type ElementType,
    int ElementBitWidth,
    int ElementCount
) : Attribute, ICLSLMetadataAttribute
{
    public Type ElementType { get; } = ElementType;
    public int ElementBitWidth { get; } = ElementBitWidth;
    public int ElementCount { get; } = ElementCount;
}
