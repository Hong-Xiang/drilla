namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class ParenthesizedExpression(IExpression Expr) : IExpression
{
}
