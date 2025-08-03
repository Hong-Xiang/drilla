using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;


public abstract record class ExpressionTree<T>
{
    public abstract ExpressionTree<TR> Select<TR>(Func<T, TR> f);
    public abstract ExpressionTree<TR> SelectMany<TR>(Func<T, ExpressionTree<TR>> f);
}

public sealed record class LeafExpression<TValue>(TValue Value) : ExpressionTree<TValue>
{
    public override ExpressionTree<TR> Select<TR>(Func<TValue, TR> f)
        => new LeafExpression<TR>(f(Value));

    public override ExpressionTree<TR> SelectMany<TR>(Func<TValue, ExpressionTree<TR>> f)
        => f(Value);
}

public sealed record class NodeExpression<TValue>(IExpression<ExpressionTree<TValue>> Node) : ExpressionTree<TValue>
{
    public override ExpressionTree<TR> Select<TR>(Func<TValue, TR> f)
        => new NodeExpression<TR>(Node.Select(e => e.Select(f)));

    public override ExpressionTree<TR> SelectMany<TR>(Func<TValue, ExpressionTree<TR>> f)
        => new NodeExpression<TR>(Node.Select(e => e.SelectMany(f)));
}

public static class ExpressionTreeExtension
{
    public static NodeExpression<T> AsNode<T>(this IExpression<ExpressionTree<T>> e) => new(e);
}

public static class SyntaxFactory<TValue>
{
    public static ExpressionTree<TValue> ValueExpr(TValue value)
        => new LeafExpression<TValue>(value);

    public static ExpressionTree<TValue> BinaryScalarExpr<TOperation>(ExpressionTree<TValue> l, ExpressionTree<TValue> r)
        where TOperation : IBinaryExpressionOperation<TOperation>
        => new BinaryExpression<TOperation, ExpressionTree<TValue>>(l, r).AsNode();

    public static ExpressionTree<TValue> LiteralExpr<TLiteral>(TLiteral literal)
        where TLiteral : ILiteral
        => new LiteralExpression<TLiteral, ExpressionTree<TValue>>(literal).AsNode();
}
