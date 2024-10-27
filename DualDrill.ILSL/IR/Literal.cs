using DualDrill.Common.Nat;
using DualDrill.ILSL.Types;

namespace DualDrill.ILSL.IR;

public interface ILiteral
{
    IType Type { get; }

}

public interface INumericLiteral : ILiteral { }

public readonly record struct BoolLiteral(bool Value) : ILiteral
{
    public IType Type => new BoolType();
}

public readonly record struct FloatLiteral<TBitWidth>(double Value)
    : INumericLiteral
    where TBitWidth : IBitWidth
{
    public IType Type => new FloatType<TBitWidth>();
}

public readonly record struct IntLiteral<TBitWidth>(long Value)
    : INumericLiteral
    where TBitWidth : IBitWidth
{
    public IType Type => new IntType<TBitWidth>();
}

public readonly record struct UIntLiteral<TBitWidth>(ulong Value)
    : INumericLiteral
    where TBitWidth : IBitWidth
{
    public IType Type => new UIntType<TBitWidth>();
}
