using DualDrill.CLSL.Language.CommonInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.LinearInstruction;

public sealed record class ConstInstruction<TLiteral>(TLiteral Literal)
    : ICommonInstruction
    where TLiteral : ILiteral
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>
        => visitor.Visit(this);

    public override string ToString() => $"const.{Literal.Type.Name} {Literal}";
}