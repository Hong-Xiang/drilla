namespace DualDrill.ILSL.IR.Expression;

public sealed record class VectorAccessExpression(IExpression Base, IExpression Index) : IExpression
{
}
