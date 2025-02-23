using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed record class BinaryStatementOperationInstruction<TOperation>
    : ISingleton<BinaryStatementOperationInstruction<TOperation>>
    , IStructuredStackInstruction
    where TOperation : IBinaryStatementOperation<TOperation>
{
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];
    public IEnumerable<Label> ReferencedLabels => [];

    public static BinaryStatementOperationInstruction<TOperation> Instance { get; } = new();

    public TOperation Operation => TOperation.Instance;

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.VisitBinaryStatement(this);

    public override string ToString() => TOperation.Instance.Name;
}