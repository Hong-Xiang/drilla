using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed class ExpressionFactorySemantic<TX, T> : IExpressionSemantic<TX, T, IExpression<T>>
{
    public IExpression<T> AddressOfChain(TX ctx, IAddressOfChainOperation operation, T e)
        => new AddressOfChainExpression<T>(operation, e);

    public IExpression<T> AddressOfIndex(TX ctx, IAddressOfChainOperation operation, T e, T index)
    {
        throw new NotImplementedException();
    }

    public IExpression<T> AddressOfSymbol(TX ctx, IAddressOfSymbolOperation operation)
        => new AddressOfSymbolExpression<T>(operation);

    public IExpression<T> Literal<TLiteral>(TX ctx, TLiteral literal)
        where TLiteral : ILiteral
        => new LiteralExpression<TLiteral, T>(literal);

    public IExpression<T> Operation1(TX ctx, IUnaryExpressionOperation operation, T e)
        => new Operation1Expression<T>(operation, e);

    public IExpression<T> Operation2(TX ctx, IBinaryExpressionOperation operation, T l, T r)
        => new Operation2Expression<T>(operation, l, r);
}

