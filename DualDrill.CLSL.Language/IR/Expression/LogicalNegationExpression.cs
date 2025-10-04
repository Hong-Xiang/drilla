namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class LogicalNegationExpression(IExpression Expr) : IExpression
{
}
