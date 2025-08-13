using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Expression;

public interface IExpressionSemantic<in TI, out TO>
{
    TO Literal<TLiteral>(TLiteral literal) where TLiteral : ILiteral;
    TO AddressOfSymbol(IAddressOfSymbolOperation operation);
    TO AddressOfChain(IAccessChainOperation operation, TI e);
    TO AddressOfIndex(IAccessChainOperation operation, TI e, TI index);
    TO Operation1(IUnaryExpressionOperation operation, TI e);
    TO Operation2(IBinaryExpressionOperation operation, TI l, TI r);
}

public interface IExpression<out T>
{
    TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic);
    IExpression<TR> Select<TR>(Func<T, TR> f);
}

public static class Expression
{
    public static IExpressionSemantic<T, IExpression<T>> Factory<T>()
        => new ExpressionFactorySemantic<T>();
}

