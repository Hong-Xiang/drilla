using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Expression;

public interface IExpressionSemantic<in TI, out TO>
{
    TO Literal<TLiteral>(TLiteral literal) where TLiteral : ILiteral;
    TO Binary<TOperation>(TI l, TI r) where TOperation : IBinaryExpressionOperation<TOperation>;
}

public interface IExpression<out T>
{
    TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic);
    IExpression<TR> Select<TR>(Func<T, TR> f);
}

