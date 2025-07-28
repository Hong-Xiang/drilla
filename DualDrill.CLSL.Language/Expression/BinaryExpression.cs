using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

public sealed record class BinaryExpression<TOperation, T>(T L, T R) : IExpression<T>
    where TOperation : IBinaryExpressionOperation<TOperation>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.Binary<TOperation>(L, R);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new BinaryExpression<TOperation, TR>(f(L), f(R));
}
