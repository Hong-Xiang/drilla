using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed record class UnaryExpressionOperationInstruction<TOperation>
    : IStructuredStackInstruction
    , ISingleton<UnaryExpressionOperationInstruction<TOperation>>
    where TOperation : IUnaryOperation<TOperation>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];
    public static UnaryExpressionOperationInstruction<TOperation> Instance { get; } = new();

    public TOperation Operation => TOperation.Instance;

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.VisitUnaryOperation(this);

    public override string ToString() => $"{Operation}";
}