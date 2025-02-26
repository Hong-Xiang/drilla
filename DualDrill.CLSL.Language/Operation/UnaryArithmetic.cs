using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public static class UnaryArithmetic
{
    public enum OpKind
    {
        neg
    }

    public interface IOp<TSelf> : IOpKind<TSelf, OpKind>, IUnaryOp<TSelf>
        where TSelf : IOp<TSelf>
    {
    }

    public sealed class Negate : IOp<Negate>, ISymbolOp<Negate>
    {
        public static OpKind Kind => OpKind.neg;

        public static Negate Instance { get; } = new();

        public string Symbol => "-";
    }
}

public sealed class UnaryNumericArithmeticExpressionOperation<TType, TOp>
    : IUnaryExpressionOperation<UnaryNumericArithmeticExpressionOperation<TType, TOp>, TType, TType, TOp>
    where TType : INumericType<TType>
    where TOp : UnaryArithmetic.IOp<TOp>
{
    public static UnaryNumericArithmeticExpressionOperation<TType, TOp> Instance { get; } = new();

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor, UnaryOperationExpression<UnaryNumericArithmeticExpressionOperation<TType, TOp>> expr)
    {
        return visitor.VisitUnaryExpression<UnaryNumericArithmeticExpressionOperation<TType, TOp>, TType, TType, TOp>(expr);
    }
}