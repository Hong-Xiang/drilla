using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Value;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorSwizzleGetOperation
{
}

public sealed class VectorSwizzleGetExpressionOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleGetExpressionOperation<TPattern, TElement>>
    , IUnaryExpressionOperation<VectorSwizzleGetExpressionOperation<TPattern, TElement>>
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public override string ToString() => Name;
    public static VectorSwizzleGetExpressionOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"get.{TPattern.Instance.Name}.{TElement.Instance.Name}";
    public IShaderType SourceType => TPattern.Instance.SourceType<TElement>().GetPtrType();
    public IShaderType ResultType => TPattern.Instance.TargetType<TElement>();
    public IExpressionValueInstruction CreateValueInstruction(IValue operand)
    {
        throw new NotImplementedException();
    }

    public IExpressionValueInstruction CreateValueInstruction(IOperationValue result, IValue operand)
    {
        throw new NotImplementedException();
    }

    public IInstruction Instruction =>
        UnaryExpressionOperationInstruction<VectorSwizzleGetExpressionOperation<TPattern, TElement>>.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<VectorSwizzleGetExpressionOperation<TPattern, TElement>> expr)
        => visitor.VisitVectorSwizzleGetExpression<TPattern, TElement>(expr);
}