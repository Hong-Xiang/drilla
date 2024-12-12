using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class LiteralValueExpression(ILiteral Literal) : IExpression
{
    public IShaderType Type => Literal.Type;
}
