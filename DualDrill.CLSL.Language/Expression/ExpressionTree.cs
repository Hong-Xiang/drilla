
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
