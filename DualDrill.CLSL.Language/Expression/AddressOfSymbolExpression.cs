using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfSymbolExpression<T>(IAddressOfSymbolOperation Operation)
    : IExpression<T>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.AddressOfSymbol(Operation);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfSymbolExpression<TR>(Operation);
}
