using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class VectorSwizzleGetOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleGetOperation<TPattern, TElement>>
    , IUnaryOperation<VectorSwizzleGetOperation<TPattern, TElement>>
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public override string ToString() => Name;
    public static VectorSwizzleGetOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"get.{TPattern.Instance.Name}.{TElement.Instance.Name}";
    public IShaderType SourceType => TPattern.Instance.SourceType<TElement>().GetPtrType();
    public IShaderType ResultType => TPattern.Instance.TargetType<TElement>();

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<VectorSwizzleGetOperation<TPattern, TElement>> expr)
        => visitor.VisitVectorSwizzleGetExpression<TPattern, TElement>(expr);
}