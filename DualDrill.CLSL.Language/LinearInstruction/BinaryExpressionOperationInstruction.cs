using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Value;
using DualDrill.CLSL.Language.ValueInstruction;
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

    public IEnumerable<IValueInstruction> CreateValueInstruction(Stack<IValue> stack)
    {
        var r = stack.Pop();
        var l = stack.Pop();
        var result = TOperation.Instance.CreateValueInstruction(l, r);
        stack.Push(result.ResultValue);
        return [result];
    }

    public override string ToString() => TOperation.Instance.Name;
}