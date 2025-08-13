using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfChainExpression<T>(IAccessChainOperation Operation, T Target)
    : IExpression<T>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.AddressOfChain(Operation, Target);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfChainExpression<TR>(Operation, f(Target));
}
