namespace DualDrill.ILSL.Types;

public readonly record struct BoolType : IScalarType, IBasicPrimitiveType<BoolType>
{
    public static BoolType Instance { get; } = new();

    public string Name => "bool";
    public int ByteSize => 4;
}
