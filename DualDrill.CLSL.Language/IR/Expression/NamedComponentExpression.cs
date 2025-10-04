namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class NamedComponentExpression(IExpression Base, string ComponentName) : IExpression
{
}
