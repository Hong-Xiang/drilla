using DualDrill.CLSL.Language.IR;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class LiteralValueExpression(ILiteral Literal) : IExpression
{
    public IShaderType Type => Literal.Type;
}
