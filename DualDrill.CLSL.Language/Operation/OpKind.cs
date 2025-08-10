using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IFloatOp<TSelf>
    where TSelf : IFloatOp<TSelf>
{
}

public interface IIntegerOp<TSelf>
    where TSelf : IIntegerOp<TSelf>
{
}

public interface ISignedIntegerOp<TSelf>
    where TSelf : ISignedIntegerOp<TSelf>
{
}

public interface IOperation
{
    FunctionDeclaration Function { get; }
    string Name { get; }
    IOperationMethodAttribute GetOperationMethodAttribute();
    IInstruction Instruction { get; }
}

public interface IOperation<TSelf> : IOperation, ISingleton<TSelf>
    where TSelf : IOperation<TSelf>
{
    IOperationMethodAttribute IOperation.GetOperationMethodAttribute()
        => new OperationMethodAttribute<TSelf>();
}

public interface IAbstractOp
{
    string Name { get; }
}

public interface IAbstractOp<TSelf> : ISingleton<TSelf>, IAbstractOp
    where TSelf : IAbstractOp<TSelf>
{
}

public interface IUnaryOp : IAbstractOp
{
}

public interface IUnaryOp<TOp> : IUnaryOp, IAbstractOp<TOp>
    where TOp : IUnaryOp<TOp>
{
}

public interface IBinaryOp : IAbstractOp
{
    IOperation GetVectorBinaryNumericOperation<TRank, TElement>()
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;

    IOperation GetScalarVectorNumericOperation<TRank, TElement>()
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;

    IOperation GetVectorScalarNumericOperation<TRank, TElement>()
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>;
}

public interface IBinaryOp<TSelf> : IBinaryOp, IAbstractOp<TSelf>
    where TSelf : IBinaryOp<TSelf>
{
    IOperation IBinaryOp.GetVectorBinaryNumericOperation<TRank, TElement>()
        => VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TSelf>.Instance;

    IOperation IBinaryOp.GetScalarVectorNumericOperation<TRank, TElement>()
        => ScalarVectorExpressionNumericOperation<TRank, TElement, TSelf>.Instance;

    IOperation IBinaryOp.GetVectorScalarNumericOperation<TRank, TElement>()
        => VectorScalarExpressionNumericOperation<TRank, TElement, TSelf>.Instance;
}

public interface ISymbolOp
{
    string Symbol { get; }
}

public interface ISymbolOp<TSelf> : ISymbolOp, ISingleton<TSelf>
    where TSelf : ISymbolOp<TSelf>
{
}

public interface IOpKind<TSelf, TOpKind> : IAbstractOp
    where TOpKind : struct, Enum
    where TSelf : IOpKind<TSelf, TOpKind>
{
    abstract static TOpKind Kind { get; }
    string IAbstractOp.Name => TSelf.Kind.ToString();
}