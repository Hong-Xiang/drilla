namespace DualDrill.ILSL.IR.Expression;

public sealed record class ParenthesizedExpression(IExpression Expr) : IExpression
{
}
