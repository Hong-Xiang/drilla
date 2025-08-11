using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

public interface IExpressionTreeVisitor<in T, out TR>
{
    TR VisitLeaf(T value);
    TR VisitNode(IExpression<IExpressionTree<T>> node);
}

public interface IExpressionTree<out T>
{
    public IExpressionTree<TR> Select<TR>(Func<T, TR> f);
    public IExpressionTree<TR> SelectMany<TR>(Func<T, IExpressionTree<TR>> f);
    public TR Accept<TR>(IExpressionTreeVisitor<T, TR> visitor);
}

public sealed record class LeafExpression<TValue>(TValue Value) : IExpressionTree<TValue>
{
    public TR Accept<TR>(IExpressionTreeVisitor<TValue, TR> visitor)
        => visitor.VisitLeaf(Value);

    public IExpressionTree<TR> Select<TR>(Func<TValue, TR> f)
        => new LeafExpression<TR>(f(Value));

    public IExpressionTree<TR> SelectMany<TR>(Func<TValue, IExpressionTree<TR>> f)
        => f(Value);
}

public sealed record class NodeExpression<TValue>(IExpression<IExpressionTree<TValue>> Node) : IExpressionTree<TValue>
{
    public TR Accept<TR>(IExpressionTreeVisitor<TValue, TR> visitor)
        => visitor.VisitNode(Node);

    public IExpressionTree<TR> Select<TR>(Func<TValue, TR> f)
        => new NodeExpression<TR>(Node.Select(e => e.Select(f)));

    public IExpressionTree<TR> SelectMany<TR>(Func<TValue, IExpressionTree<TR>> f)
        => new NodeExpression<TR>(Node.Select(e => e.SelectMany(f)));
}

public interface IExpressionTreeFoldSemantic<in TX, TValue, T>
    : IExpressionSemantic<TX, T, T>
{
    T Value(TX context, TValue value);
}



public static class ExpressionTreeExtension
{
    public static NodeExpression<T> AsNode<T>(this IExpression<IExpressionTree<T>> e) => new(e);

    public static TR Fold<TX, T, TR>(this IExpressionTree<T> e, IExpressionTreeFoldSemantic<TX, T, TR> semantic, TX context)
    {
        return e switch
        {
            LeafExpression<T> leaf => semantic.Value(context, leaf.Value),
            NodeExpression<T> node => node.Node.Select(e => e.Fold(semantic, context)).Evaluate(semantic, context),
            _ => throw new InvalidOperationException("Unknown expression tree type")
        };
    }
}

public abstract class ExpressionTree
{
    public static IExpressionTree<T> Value<T>(T value) => new LeafExpression<T>(value);
}

public static class SyntaxFactory<TValue>
{
    public static IExpressionTree<TValue> LiteralExpr<TLiteral>(TLiteral literal)
        where TLiteral : ILiteral
        => new LiteralExpression<TLiteral, IExpressionTree<TValue>>(literal).AsNode();
}