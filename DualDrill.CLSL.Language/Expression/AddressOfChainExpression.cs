using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfChainExpression<T>(IAddressOfChainOperation Operation, T Target)
    : IExpression<T>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.AddressOfChain(ctx, Operation, Target);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfChainExpression<TR>(Operation, f(Target));
}
