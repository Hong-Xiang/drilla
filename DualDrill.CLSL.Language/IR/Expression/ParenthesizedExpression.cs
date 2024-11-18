using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class ParenthesizedExpression(IExpression Expr) : IExpression
{
    public IShaderType Type => Expr.Type;
}
