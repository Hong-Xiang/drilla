using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfIndexExpression<T>(IAccessChainOperation Operation, T Target, T Index)
    : IExpression<T>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.AddressOfIndex(ctx, Operation, Target, Index);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfIndexExpression<TR>(Operation, f(Target), f(Index));
}
