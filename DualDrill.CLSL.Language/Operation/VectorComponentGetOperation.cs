using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

// TODO: change math code gen to use <TRank, TElement, TComponent> generic parameters
public sealed class VectorComponentGetExpressionOperation<TRank, TVector, TComponent>
    : IUnaryExpressionOperation<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentGetExpressionOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public string Name => $"get.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    public IShaderType SourceType => TVector.Instance.GetPtrType();
    public IShaderType ResultType => TVector.Instance.ElementType;

    IUnaryExpression IUnaryExpressionOperation.CreateExpression(IExpression expr)
        => new UnaryOperationExpression<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>(expr);

    record struct ValueInstructionVisitor(
        IOperationValue Result,
        IValue<TVector> Operand
    )
        : IShaderTypeVisitor1<IExpressionValueInstruction>
    {
        public IExpressionValueInstruction Visit<TType>(TType type) where TType : IShaderType<TType>
        {
            return new ExpressionOperation1ValueInstruction<
                VectorComponentGetExpressionOperation<TRank, TVector, TComponent>,
                TVector,
                TType
            >((OperationValue<TType>)Result, Operand);
        }
    }

    public IExpressionValueInstruction CreateValueInstruction(IOperationValue result, IValue operand)
    {
        if (result.Type != TVector.Instance.ElementType)
        {
            throw new ArgumentException(
                $"The result type is expected to be{TVector.Instance.ElementType.Name}, got {result.Type.Name}",
                nameof(result));
        }

        if (operand is not IValue<TVector> o)
        {
            throw new ArgumentException(
                $"The operand type is expected {TVector.Instance.Name}, got {operand.Type.Name}",
                nameof(result));
        }

        return TVector.Instance.ElementType.Accept<ValueInstructionVisitor, IExpressionValueInstruction>(
            new ValueInstructionVisitor(result, o));
    }

    public IExpressionValueInstruction CreateValueInstruction(IValue operand)
    {
        throw new NotImplementedException();
    }

    public IInstruction Instruction =>
        UnaryExpressionOperationInstruction<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>> expr)
        => visitor.VisitVectorComponentGetExpression<TRank, TVector, TComponent>(expr);
}