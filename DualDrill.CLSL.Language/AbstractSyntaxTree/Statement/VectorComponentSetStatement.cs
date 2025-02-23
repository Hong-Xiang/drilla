using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class VectorComponentSetStatement<TRank, TElement, TComponent>(
    IExpression Target,
    IExpression Value
) : ICommonStatement
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public IEnumerable<Label> ReferencedLabels => [];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
    [
        ..Target.ReferencedVariables,
        ..Value.ReferencedVariables
    ];

    public IEnumerable<IStackInstruction> ToInstructions()
        =>
        [
            ..Target.ToInstructions(),
            ..Value.ToInstructions(),
            ((IOperation)VectorComponentSetOperation<TRank, VecType<TRank, TElement>, TComponent>.Instance).Instruction
        ];

    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitVectorComponentSet(this);
}