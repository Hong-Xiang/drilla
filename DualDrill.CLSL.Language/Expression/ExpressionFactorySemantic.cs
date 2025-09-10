using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;

namespace DualDrill.CLSL.Language.Expression;

sealed class ExpressionFactorySemantic<T> : IExpressionSemantic<T, IExpression<T>>
{
    public IExpression<T> AddressOfChain(IAccessChainOperation operation, T e)
        => new AddressOfChainExpression<T>(operation, e);

    public IExpression<T> AddressOfIndex(IAccessChainOperation operation, T e, T index)
        => new AddressOfIndexExpression<T>(operation, e, index);

    public IExpression<T> Literal<TLiteral>(TLiteral literal)
        where TLiteral : ILiteral
        => new LiteralExpression<TLiteral, T>(literal);

    public IExpression<T> Operation1(IUnaryExpressionOperation operation, T e)
        => new Operation1Expression<T>(operation, e);

    public IExpression<T> Operation2(IBinaryExpressionOperation operation, T l, T r)
        => new Operation2Expression<T>(operation, l, r);

    public IExpression<T> VectorCompositeConstruction(VectorCompositeConstructionOperation operation, IEnumerable<T> arguments)
        => new VectorCompositeConstructExpression<T>(operation, [.. arguments]);
}

