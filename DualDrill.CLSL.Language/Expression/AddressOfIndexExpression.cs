using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfIndexExpression<T>(IAccessChainOperation Operation, T Target, T Index)
    : IExpression<T>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.AddressOfIndex(Operation, Target, Index);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfIndexExpression<TR>(Operation, f(Target), f(Index));
}
