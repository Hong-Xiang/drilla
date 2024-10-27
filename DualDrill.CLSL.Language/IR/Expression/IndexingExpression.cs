namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class IndexingExpression(IExpression Base, IExpression Index) : IExpression
{
}
