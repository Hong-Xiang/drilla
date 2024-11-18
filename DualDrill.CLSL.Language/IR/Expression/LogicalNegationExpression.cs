using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class LogicalNegationExpression(IExpression Expr) : IExpression
{
    public IShaderType Type => Expr.Type;
}
