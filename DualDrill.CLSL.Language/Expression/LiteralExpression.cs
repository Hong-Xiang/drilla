using DualDrill.CLSL.Language.Literal;

namespace DualDrill.CLSL.Language.Expression;

public interface ILiteralExpression
{
    ILiteral LiteralValue { get; }
}

public sealed record class LiteralExpression<TLiteral, T>(TLiteral Literal)
    : IExpression<T>, ILiteralExpression
    where TLiteral : ILiteral
{
    public ILiteral LiteralValue => Literal;

    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.Literal(Literal);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new LiteralExpression<TLiteral, TR>(Literal);

    public override string ToString()
        => $"lit({Literal})";
}
