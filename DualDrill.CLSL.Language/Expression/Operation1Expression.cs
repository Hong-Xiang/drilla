using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

sealed record class Operation1Expression<T>(IUnaryExpressionOperation Operation, T Expression)
    : IExpression<T>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.Operation1(Operation, Expression);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new Operation1Expression<TR>(Operation, f(Expression));
}
