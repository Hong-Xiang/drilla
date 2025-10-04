using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IBitCastOperation : IUnaryExpressionOperation
{
}

internal sealed class ScalarBitCastOperation<TSource, TTarget>
    : IBitCastOperation
    , IUnaryExpressionOperation<ScalarBitCastOperation<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public string Name => $"bitcast.{TSource.Instance.Name}.{TTarget.Instance.Name}";

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context) =>
        throw new NotImplementedException();

    public IShaderType SourceType => throw new NotImplementedException();

    public IShaderType ResultType => throw new NotImplementedException();
    public static ScalarBitCastOperation<TSource, TTarget> Instance { get; } = new();

    public override string ToString() => Name;
}