using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfSymbolExpression<T>(IAddressOfSymbolOperation Operation)
    : IExpression<T>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.AddressOfSymbol(ctx, Operation);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfSymbolExpression<TR>(Operation);
}
