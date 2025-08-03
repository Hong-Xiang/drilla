namespace DualDrill.CLSL.Language.Expression;

public interface IExprTransform<in TS, out TR>
{
    TR MapExpr(TS expr);
}

public static class ExprTransform
{
    sealed class FuncExprTransform<TS, TR>(Func<TS, TR> f) : IExprTransform<TS, TR>
    {
        public TR MapExpr(TS expr) => f(expr);
    }

    public static IExprTransform<TS, TR> Create<TS, TR>(Func<TS, TR> f)
    {
        return new FuncExprTransform<TS, TR>(f);
    }

    public static IExprTransform<ExpressionTree<T>, T> Create<T>(Func<ExpressionTree<T>, T> f)
    {
        return new FuncExprTransform<ExpressionTree<T>, T>(f);
    }
}
