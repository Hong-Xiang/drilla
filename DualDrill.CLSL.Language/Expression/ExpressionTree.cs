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

public interface IExpressionTreeFoldSemantic<TValue, T>
    : IExpressionSemantic<T, T>
{
    T Value(TValue value);
}

public interface IExpressionTreeLazyFoldSemantic<TValue, T>
    : IExpressionSemantic<Func<T>, T>
{
    T Value(TValue value);
}

public static class ExpressionTreeExtension
{
    public static NodeExpression<T> AsNode<T>(this IExpression<IExpressionTree<T>> e) => new(e);

    public static TR Fold<T, TR>(this IExpressionTree<T> e, IExpressionTreeFoldSemantic<T, TR> semantic)
    {
        return e switch
        {
            LeafExpression<T> leaf => semantic.Value(leaf.Value),
            NodeExpression<T> node => node.Node.Select(e => e.Fold(semantic)).Evaluate(semantic),
            _ => throw new InvalidOperationException("Unknown expression tree type")
        };
    }

    sealed class FoldLazyImplSemantic<TValue, T>(IExpressionTreeLazyFoldSemantic<TValue, T> semantic)
        : IExpressionSemantic<IExpressionTree<TValue>, T>
        , IExpressionTreeVisitor<TValue, T>
    {
        public T AddressOfChain(IAccessChainOperation operation, IExpressionTree<TValue> e)
            => semantic.AddressOfChain(operation, () => e.Accept(this));

        public T AddressOfIndex(IAccessChainOperation operation, IExpressionTree<TValue> e, IExpressionTree<TValue> index)
            => semantic.AddressOfIndex(operation, () => e.Accept(this), () => index.Accept(this));

        public T AddressOfSymbol(IAddressOfSymbolOperation operation)
            => semantic.AddressOfSymbol(operation);

        public T Literal<TLiteral>(TLiteral literal) where TLiteral : ILiteral
            => semantic.Literal(literal);

        public T Operation1(IUnaryExpressionOperation operation, IExpressionTree<TValue> e)
            => semantic.Operation1(operation, () => e.Accept(this));

        public T Operation2(IBinaryExpressionOperation operation, IExpressionTree<TValue> l, IExpressionTree<TValue> r)
            => semantic.Operation2(operation, () => l.Accept(this), () => r.Accept(this));

        public T VisitLeaf(TValue value)
            => semantic.Value(value);

        public T VisitNode(IExpression<IExpressionTree<TValue>> node)
            => node.Select<Func<T>>(e => () => e.Accept(this)).Evaluate(semantic);
    }

    public static TR Fold<T, TR>(this IExpressionTree<T> e, IExpressionTreeLazyFoldSemantic<T, TR> semantic)
    {
        return e.Accept(new FoldLazyImplSemantic<T, TR>(semantic));
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