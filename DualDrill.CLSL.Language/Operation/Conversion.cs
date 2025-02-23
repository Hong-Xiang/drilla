using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public interface IConversionOperation : IUnaryExpressionOperation
{
}

public sealed class ScalarConvOp<TSource, TTarget> : IUnaryOp<ScalarConvOp<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public string Name => $"conv.{TSource.Instance.Name}.{TTarget.Instance.Name}";
    public static ScalarConvOp<TSource, TTarget> Instance { get; } = new();
}

public sealed class ScalarConversionOperation<TSource, TTarget>
    : IConversionOperation
    , IUnaryExpressionOperation<ScalarConversionOperation<TSource, TTarget>, TSource, TTarget, ScalarConvOp<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public static ScalarConversionOperation<TSource, TTarget> Instance { get; } = new();


    public string Name => ScalarConvOp<TSource, TTarget>.Instance.Name;

    public override string ToString() => Name;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<ScalarConversionOperation<TSource, TTarget>> expr)
    {
        throw new NotImplementedException();
    }
}