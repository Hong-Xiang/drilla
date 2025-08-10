using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

public sealed record class Operation2Expression<T>(IBinaryExpressionOperation Operation, T L, T R) : IExpression<T>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.Operation2(ctx, Operation, L, R);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new Operation2Expression<TR>(Operation, f(L), f(R));
}
