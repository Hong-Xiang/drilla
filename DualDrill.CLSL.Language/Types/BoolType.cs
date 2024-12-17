using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed class BoolType : IScalarType<BoolType>, IBasicPrimitiveType<BoolType>
{
    public static BoolType Instance { get; } = new();

    public string Name => "bool";
    public int ByteSize => 4;

    public IBitWidth BitWidth { get; } = N8.Instance;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T>
        => visitor.Visit(this);
}
