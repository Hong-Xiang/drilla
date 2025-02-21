using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
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
    public static VectorNumericUnaryOperation<TRank, TElement, TOp> Instance { get; } = new();

    private VectorNumericUnaryOperation()
    {
    }

    public VecType<TRank, TElement> OperandType => VecType<TRank, TElement>.Instance;


    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        IUnaryExpressionOperation<VectorNumericUnaryOperation<TRank, TElement, TOp>, VecType<TRank, TElement>,
            VecType<TRank, TElement>, TOp>.Expression expr)
    {
        throw new NotImplementedException();
    }
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
    public static VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp> Instance { get; } = new();

    private VectorExpressionNumericBinaryExpressionOperation()
    {
    }

    public VecType<TRank, TElement> OperandType => VecType<TRank, TElement>.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        IBinaryExpressionOperation<VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp>,
            VecType<TRank, TElement>, VecType<TRank, TElement>, VecType<TRank, TElement>, TOp>.Expression expr)
    {
        throw new NotImplementedException();
    }
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
    public static ScalarVectorExpressionNumericOperation<TRank, TElement, TOp> Instance { get; } = new();

    private ScalarVectorExpressionNumericOperation()
    {
    }

    public VecType<TRank, TElement> VectorType => VecType<TRank, TElement>.Instance;
    public IScalarType ScalarType => TElement.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        IBinaryExpressionOperation<ScalarVectorExpressionNumericOperation<TRank, TElement, TOp>, TElement,
            VecType<TRank, TElement>, VecType<TRank, TElement>, TOp>.Expression expr)
    {
        throw new NotImplementedException();
    }
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
    public static VectorScalarExpressionNumericOperation<TRank, TElement, TOp> Instance { get; } = new();

    private VectorScalarExpressionNumericOperation()
    {
    }

    public VecType<TRank, TElement> VectorType => VecType<TRank, TElement>.Instance;
    public IScalarType ScalarType => TElement.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        IBinaryExpressionOperation<VectorScalarExpressionNumericOperation<TRank, TElement, TOp>,
            VecType<TRank, TElement>, TElement, VecType<TRank, TElement>, TOp>.Expression expr)
    {
        throw new NotImplementedException();
    }
}