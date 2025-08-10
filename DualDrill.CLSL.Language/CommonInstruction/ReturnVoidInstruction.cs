using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.CommonInstruction;

public sealed record class ReturnVoidInstruction() : ITermintorCommonInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => throw new NotImplementedException();

    public ISuccessor ToSuccessor()
        => Successor.Terminate();

    public override string ToString() => "return";

    public ITerminator<Label, Unit> ToTerminator()
        => Terminator.B.ReturnVoid<Label, Unit>();
}