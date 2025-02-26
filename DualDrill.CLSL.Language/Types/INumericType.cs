using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface INumericType : IScalarType
{
    INumericBinaryArithmeticOperation ArithmeticOperation<TOp>() where TOp : BinaryArithmetic.IOp<TOp>;
    INumericBinaryRelationalOperation RelationalOperation<TOp>() where TOp : BinaryRelational.IOp<TOp>;

    IOperation GetVectorUnaryNumericOperation<TRank, TOp>()
        where TRank : IRank<TRank>
        where TOp : IUnaryOp<TOp>;

    IUnaryExpressionOperation UnaryArithmeticOperation<TOp>() where TOp : UnaryArithmetic.IOp<TOp>;
}

public interface INumericType<TSelf> : INumericType, IScalarType<TSelf>
    where TSelf : INumericType<TSelf>
{

    IUnaryExpressionOperation INumericType.UnaryArithmeticOperation<TOp>()
        => UnaryNumericArithmeticExpressionOperation<TSelf, TOp>.Instance;
    IOperation INumericType.GetVectorUnaryNumericOperation<TRank, TOp>()
        => VectorNumericUnaryOperation<TRank, TSelf, TOp>.Instance;

    INumericBinaryArithmeticOperation INumericType.ArithmeticOperation<TOp>() =>
        NumericBinaryArithmeticOperation<TSelf, TOp>.Instance;

    INumericBinaryRelationalOperation INumericType.RelationalOperation<TOp>() =>
        NumericBinaryRelationalOperation<TSelf, TOp>.Instance;
}