using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorFromScalarConstructOperation : IUnaryExpressionOperation
{
    IScalarType ElementType { get; }
    IRank Size { get; }
}

public sealed class VectorFromScalarConstructOperation<TRank, TElement>
    : IUnaryExpressionOperation<VectorFromScalarConstructOperation<TRank, TElement>>
    , IVectorFromScalarConstructOperation
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
{
    public static VectorFromScalarConstructOperation<TRank, TElement> Instance { get; } = new();

    public IShaderType SourceType => TElement.Instance;

    public IShaderType ResultType => VecType<TRank, TElement>.Instance;

    public string Name => $"{ResultType.Name}.ctor({SourceType.Name})";

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context)
        => semantic.VectorFromScalarConstruct(context, this);

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor, UnaryOperationExpression<VectorFromScalarConstructOperation<TRank, TElement>> expr)
        => throw new NotImplementedException();

    public FunctionDeclaration Function => new(
        $"vec{TRank.Instance.Value}",
        [
            new ParameterDeclaration("s", SourceType, []),
        ],
        new FunctionReturn(ResultType, []),
        [new ShaderRuntimeMethodAttribute(), new OperationMethodAttribute<VectorFromScalarConstructOperation<TRank, TElement>>()]
    );

    public IScalarType ElementType => TElement.Instance;

    public IRank Size => TRank.Instance;
}
