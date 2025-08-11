using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Expression;

sealed class ExpressionFactorySemantic<T> : IExpressionSemantic<Unit, T, IExpression<T>>
{
    public IExpression<T> AddressOfChain(Unit ctx, IAccessChainOperation operation, T e)
        => new AddressOfChainExpression<T>(operation, e);

    public IExpression<T> AddressOfIndex(Unit ctx, IAccessChainOperation operation, T e, T index)
        => new AddressOfIndexExpression<T>(operation, e, index);

    public IExpression<T> AddressOfSymbol(Unit ctx, IAddressOfSymbolOperation operation)
        => new AddressOfSymbolExpression<T>(operation);

    public IExpression<T> Literal<TLiteral>(Unit ctx, TLiteral literal)
        where TLiteral : ILiteral
        => new LiteralExpression<TLiteral, T>(literal);

    public IExpression<T> Operation1(Unit ctx, IUnaryExpressionOperation operation, T e)
        => new Operation1Expression<T>(operation, e);

    public IExpression<T> Operation2(Unit ctx, IBinaryExpressionOperation operation, T l, T r)
        => new Operation2Expression<T>(operation, l, r);
}

