using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

sealed record class Operation1Expression<T>(IUnaryExpressionOperation Operation, T Expression)
    : IExpression<T>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.Operation1(ctx, Operation, Expression);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new Operation1Expression<TR>(Operation, f(Expression));
}
