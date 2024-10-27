namespace DualDrill.CLSL.Language.Types;

public sealed record class BoolType : IScalarType, IBasicPrimitiveType<BoolType>
{
    BoolType() { }
    public static BoolType Instance { get; } = new();

    public string Name => "bool";
    public int ByteSize => 4;
}
