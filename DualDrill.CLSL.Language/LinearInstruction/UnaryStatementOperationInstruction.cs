using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed class UnaryStatementOperationInstruction<TOperation>
    : IInstruction
    , ISingleton<UnaryStatementOperationInstruction<TOperation>>
    where TOperation : IUnaryStatementOperation<TOperation>
{
    public static UnaryStatementOperationInstruction<TOperation> Instance { get; } = new();
    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
    {
        throw new NotImplementedException();
    }

}