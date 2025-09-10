using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IBitCastOperation : IUnaryExpressionOperation
{
}

sealed class ScalarBitCastOperation<TSource, TTarget>
   : IBitCastOperation
   , IUnaryExpressionOperation<ScalarBitCastOperation<TSource, TTarget>>
   where TSource : IScalarType<TSource>
   where TTarget : IScalarType<TTarget>
{
    public static ScalarBitCastOperation<TSource, TTarget> Instance { get; } = new();
    public string Name => $"bitcast.{TSource.Instance.Name}.{TTarget.Instance.Name}";
    public override string ToString() => Name;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<ScalarBitCastOperation<TSource, TTarget>> expr)
        => visitor.VisitConversionExpression<TTarget>(expr);

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context)
    {
        throw new NotImplementedException();
    }

    public IInstruction Instruction =>
        UnaryExpressionOperationInstruction<ScalarBitCastOperation<TSource, TTarget>>.Instance;

    public IShaderType SourceType => throw new NotImplementedException();

    public IShaderType ResultType => throw new NotImplementedException();
}