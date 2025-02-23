using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public interface IBitCastOperation : IUnaryExpressionOperation
{
}

public sealed class ScalarBitCastOp<TSource, TTarget> : IUnaryOp<ScalarBitCastOp<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public string Name => $"conv.{TSource.Instance.Name}.{TTarget.Instance.Name}";
    public static ScalarBitCastOp<TSource, TTarget> Instance { get; } = new();
}

public sealed class ScalarBitCastOperation<TSource, TTarget>
    : IConversionOperation
    , IUnaryExpressionOperation<ScalarBitCastOperation<TSource, TTarget>, TSource, TTarget, ScalarBitCastOp<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public static ScalarBitCastOperation<TSource, TTarget> Instance { get; } = new();
    public string Name => ScalarBitCastOp<TSource, TTarget>.Instance.Name;
    public override string ToString() => Name;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<ScalarBitCastOperation<TSource, TTarget>> expr)
        => visitor.VisitConversionExpression<TTarget>(expr);
}