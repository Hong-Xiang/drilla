using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorNumericOperation : IOperation
{
}

public interface IVectorBinaryNumericOperation : IBinaryExpressionOperation, IVectorNumericOperation
{
}

public interface IVectorNumericOperation<TSelf>
    : IOperation<TSelf>
    , IVectorNumericOperation
    where TSelf : IVectorNumericOperation<TSelf>
{
}

public interface IVectorBinaryExpressionNumericOperation<TSelf>
    : IVectorNumericOperation<TSelf>
    , IBinaryExpressionOperation<TSelf>
    where TSelf : IVectorBinaryExpressionNumericOperation<TSelf>
{
}

public sealed class VectorNumericUnaryOperation<TRank, TElement, TOp>
    : IVectorNumericOperation<VectorNumericUnaryOperation<TRank, TElement, TOp>>
    , IUnaryExpressionOperation<VectorNumericUnaryOperation<TRank, TElement, TOp>,
          VecType<TRank, TElement>,
          VecType<TRank, TElement>,
          TOp>
    where TRank : IRank<TRank>
    where TElement : INumericType<TElement>
    // TODO: use more restricted constraint
    where TOp : IUnaryOp<TOp>

{
    private VectorNumericUnaryOperation()
    {
    }

    public VecType<TRank, TElement> OperandType => VecType<TRank, TElement>.Instance;


    public static VectorNumericUnaryOperation<TRank, TElement, TOp> Instance { get; } = new();

}

public sealed class VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp>
    : IVectorBinaryExpressionNumericOperation<VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp>>
    , IVectorBinaryNumericOperation
    , IBinaryExpressionOperation<VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp>,
          VecType<TRank, TElement>,
          VecType<TRank, TElement>,
          VecType<TRank, TElement>,
          TOp
      >
    where TRank : IRank<TRank>
    // TODO: fix this to INumericType<TElement>
    where TElement : IScalarType<TElement>
    where TOp : IBinaryOp<TOp>

{
    private VectorExpressionNumericBinaryExpressionOperation()
    {
    }

    public VecType<TRank, TElement> OperandType => VecType<TRank, TElement>.Instance;
    public static VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp> Instance { get; } = new();
}

public sealed class ScalarVectorExpressionNumericOperation<TRank, TElement, TOp>
    : IVectorBinaryExpressionNumericOperation<ScalarVectorExpressionNumericOperation<TRank, TElement, TOp>>
    , IBinaryExpressionOperation<ScalarVectorExpressionNumericOperation<TRank, TElement, TOp>,
          TElement,
          VecType<TRank, TElement>,
          VecType<TRank, TElement>,
          TOp>
    , IVectorBinaryNumericOperation
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
    where TOp : IBinaryOp<TOp>

{
    private ScalarVectorExpressionNumericOperation()
    {
    }

    public VecType<TRank, TElement> VectorType => VecType<TRank, TElement>.Instance;
    public IScalarType ScalarType => TElement.Instance;
    public static ScalarVectorExpressionNumericOperation<TRank, TElement, TOp> Instance { get; } = new();
}

public sealed class VectorScalarExpressionNumericOperation<TRank, TElement, TOp>
    : IVectorBinaryExpressionNumericOperation<VectorScalarExpressionNumericOperation<TRank, TElement, TOp>>
    , IBinaryExpressionOperation<
          VectorScalarExpressionNumericOperation<TRank, TElement, TOp>,
          VecType<TRank, TElement>,
          TElement,
          VecType<TRank, TElement>,
          TOp>
    , IVectorBinaryNumericOperation
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
    where TOp : IBinaryOp<TOp>
{
    private VectorScalarExpressionNumericOperation()
    {
    }

    public VecType<TRank, TElement> VectorType => VecType<TRank, TElement>.Instance;
    public IScalarType ScalarType => TElement.Instance;
    public static VectorScalarExpressionNumericOperation<TRank, TElement, TOp> Instance { get; } = new();
}