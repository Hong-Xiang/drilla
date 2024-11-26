using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed record class BoolType : IScalarType, IBasicPrimitiveType<BoolType>
{
    BoolType() { }
    public static BoolType Instance { get; } = new();

    public string Name => "bool";
    public int ByteSize => 4;

    public IBitWidth BitWidth { get; } = N8.Instance;

    public string ElementTypeName => "b";
}
