namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class VectorAccessExpression(IExpression Base, IExpression Index) : IExpression
{
}
