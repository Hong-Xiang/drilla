using DualDrill.CLSL.Language.CommonInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Value;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed record class ConstInstruction<TLiteral>(TLiteral Literal)
    : ICommonInstruction
    where TLiteral : ILiteral
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public IEnumerable<IValueInstruction> CreateValueInstruction(Stack<IValue> stack)
    {
        var r = Literal.CreateValueInstruction();
        stack.Push(r.ResultValue);
        return [r];
    }

    public override string ToString() => $"const.{Literal.Type.Name} {Literal}";
}