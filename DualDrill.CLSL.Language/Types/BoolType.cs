using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.Nat;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

[DebuggerDisplay("{Name}")]
public sealed class BoolType : IScalarType<BoolType>, IBasicPrimitiveType<BoolType>
{
    public static BoolType Instance { get; } = new();

    public string Name => "bool";

    public int ByteSize => 4;

    public IBitWidth BitWidth { get; } = N8.Instance;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T>
        => visitor.Visit(this);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic)
        => semantic.BoolType(this);
}