using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class ParenthesizedExpression(IExpression Expr) : IExpression
{
    public IShaderType Type => Expr.Type;
}
