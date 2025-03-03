using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.Nat;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

[DebuggerDisplay("{Name}")]
public sealed class BoolType : IScalarType<BoolType>, IBasicPrimitiveType<BoolType>
{
    public static BoolType Instance { get; } = new();

    public string Name => "bool";
    
    static IPtrType PtrType { get; } = new PtrType(Instance);
    public IPtrType GetPtrType() => PtrType;
    

    public int ByteSize => 4;

    public IBitWidth BitWidth { get; } = N8.Instance;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T>
        => visitor.Visit(this);
}