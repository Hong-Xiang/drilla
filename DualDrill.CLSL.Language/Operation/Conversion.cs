using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IConversionOperation : IUnaryExpressionOperation
{
}

public sealed class ScalarConversionOperation<TSource, TTarget>
    : IConversionOperation
    , IUnaryExpressionOperation<ScalarConversionOperation<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public static ScalarConversionOperation<TSource, TTarget> Instance { get; } = new();
    public string Name => $"conv.{TSource.Instance.Name}.{TTarget.Instance.Name}";
    public override string ToString() => Name;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<ScalarConversionOperation<TSource, TTarget>> expr)
        => visitor.VisitConversionExpression<TTarget>(expr);

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context)
    {
        throw new NotImplementedException();
    }

    public IInstruction Instruction =>
        UnaryExpressionOperationInstruction<ScalarConversionOperation<TSource, TTarget>>.Instance;

    public IShaderType SourceType => throw new NotImplementedException();

    public IShaderType ResultType => throw new NotImplementedException();
}