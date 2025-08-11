using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Expression;

public interface IExpressionSemantic<in TX, in TI, out TO>
{
    TO Literal<TLiteral>(TX ctx, TLiteral literal) where TLiteral : ILiteral;
    TO AddressOfSymbol(TX ctx, IAddressOfSymbolOperation operation);
    TO AddressOfChain(TX ctx, IAccessChainOperation operation, TI e);
    TO AddressOfIndex(TX ctx, IAccessChainOperation operation, TI e, TI index);
    TO Operation1(TX ctx, IUnaryExpressionOperation operation, TI e);
    TO Operation2(TX ctx, IBinaryExpressionOperation operation, TI l, TI r);
}

public interface IExpression<out T>
{
    TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx);
    IExpression<TR> Select<TR>(Func<T, TR> f);
}

public static class Expression
{
    public static IExpressionSemantic<Unit, T, IExpression<T>> Factory<T>()
        => new ExpressionFactorySemantic<T>();
}

