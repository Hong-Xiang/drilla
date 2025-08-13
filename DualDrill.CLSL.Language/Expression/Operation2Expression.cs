using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

public sealed record class Operation2Expression<T>(IBinaryExpressionOperation Operation, T L, T R) : IExpression<T>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.Operation2(Operation, L, R);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new Operation2Expression<TR>(Operation, f(L), f(R));
}
