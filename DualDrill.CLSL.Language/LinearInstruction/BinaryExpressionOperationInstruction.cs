using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed record class BinaryExpressionOperationInstruction<TOperation>
    : ISingleton<BinaryExpressionOperationInstruction<TOperation>>
    , IOperationStackInstruction
    where TOperation : IBinaryExpressionOperation<TOperation>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static BinaryExpressionOperationInstruction<TOperation> Instance { get; } = new();
    public IOperation Operation => TOperation.Instance;

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => TOperation.Instance.Name;
}