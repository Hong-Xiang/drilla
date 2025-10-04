using DualDrill.Common.Nat;
using DualDrill.ILSL.IR.Declaration;

namespace DualDrill.ILSL.Types;

public readonly record struct IntType<TBitWidth>() : IIntegerType, IScalarType<IntType<TBitWidth>>
    where TBitWidth : IBitWidth
{

    public readonly static FunctionDeclaration Cast = new FunctionDeclaration(
           new IntType<TBitWidth>().Name,
           [],
           new FunctionReturn(new IntType<TBitWidth>(), []),
           []
       );

    public static IntType<TBitWidth> Instance { get; } = new();

    public string Name => $"i{Nat.GetInstance<TBitWidth>().Value}";

    public int ByteSize => Nat.GetInstance<TBitWidth>().Value / 8;
}
