using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed record class UnaryExpressionOperationInstruction<TOperation>
    : IInstruction
    , ISingleton<UnaryExpressionOperationInstruction<TOperation>>
    where TOperation : IUnaryExpressionOperation<TOperation>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];
    public static UnaryExpressionOperationInstruction<TOperation> Instance { get; } = new();

    public TOperation Operation => TOperation.Instance;

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.VisitUnaryOperation(this);

    public override string ToString() => $"{Operation.Name}";
}