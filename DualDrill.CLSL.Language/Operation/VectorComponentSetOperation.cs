using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorComponentSetOperation : IOperation
{
    IStackStatement CreateStatement(IExpression target, IExpression value);
}

public sealed class VectorComponentSetOperation<TRank, TVector, TComponent>
    : IVectorComponentSetOperation
    , IOperation<VectorComponentSetOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentSetOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public FunctionDeclaration Function { get; } = new FunctionDeclaration(
        $"set_{TComponent.Instance.Name}_{TVector.Instance.Name}",
        [
            new ParameterDeclaration("v", TVector.Instance.GetPtrType(), []),
            new ParameterDeclaration("value", TVector.Instance.ElementType, [])
        ],
        new FunctionReturn(UnitType.Instance, []),
        [new OperationMethodAttribute<VectorComponentSetOperation<TRank, TVector, TComponent>>()]);

    public string Name => $"set.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    IInstruction IOperation.Instruction => Instruction.Instance;

    public sealed class Instruction
        : ISingleton<Instruction>
        , IInstruction
    {
        public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

        public IEnumerable<Label> ReferencedLabels => [];

        public static Instruction Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : IStructuredStackInstructionVisitor<TResult>
            => visitor.VisitVectorComponentSet<TRank, TVector, TComponent>();
    }

    record struct SizedVecStmtVisitor(IExpression Target, IExpression Value)
        : ISizedVecType<TRank, TVector>.ISizedVisitor<IStackStatement>
    {
        public IStackStatement Visit<TElement>(VecType<TRank, TElement> t) where TElement : IScalarType<TElement>
            => new VectorComponentSetStatement<TRank, TElement, TComponent>(Target, Value);
    }

    public IStackStatement CreateStatement(IExpression target, IExpression value)
        => TVector.Instance.Accept(new SizedVecStmtVisitor(target, value));
}