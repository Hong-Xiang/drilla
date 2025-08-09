using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

public sealed record class BinaryExpression<TOperation, T>(T L, T R) : IExpression<T>
    where TOperation : IBinaryExpressionOperation<TOperation>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.Binary<TOperation>(ctx, L, R);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new BinaryExpression<TOperation, TR>(f(L), f(R));
}
