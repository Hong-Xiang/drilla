using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
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
    public string Name => $"conv.{TSource.Instance.Name}.{TTarget.Instance.Name}";

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context) =>
        throw new NotImplementedException();


    public IShaderType SourceType => TSource.Instance;

    public IShaderType ResultType => TTarget.Instance;
    public static ScalarConversionOperation<TSource, TTarget> Instance { get; } = new();


    public override string ToString() => Name;
}