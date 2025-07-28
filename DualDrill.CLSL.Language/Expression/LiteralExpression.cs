using DualDrill.CLSL.Language.Literal;

namespace DualDrill.CLSL.Language.Expression;

public sealed record class LiteralExpression<TLiteral, T>(TLiteral Literal) : IExpression<T>
    where TLiteral : ILiteral
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.Literal(Literal);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new LiteralExpression<TLiteral, TR>(Literal);
}
