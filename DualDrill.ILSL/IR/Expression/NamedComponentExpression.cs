namespace DualDrill.ILSL.IR.Expression;

public sealed record class NamedComponentExpression(IExpression Base, string ComponentName) : IExpression
{
}
