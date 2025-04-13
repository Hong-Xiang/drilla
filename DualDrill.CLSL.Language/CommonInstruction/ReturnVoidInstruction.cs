using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Value;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.CommonInstruction;

public sealed record class ReturnVoidInstruction() : ITermintorCommonInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => throw new NotImplementedException();

    public IEnumerable<IValueInstruction> CreateValueInstruction(Stack<IValue> stack)
        => [this];

    public ISuccessor ToSuccessor()
        => Successor.Terminate();

    public override string ToString() => "return";
}