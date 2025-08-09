using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Expression;

public interface IExpressionSemantic<in TX, in TI, out TO>
{
    TO Literal<TLiteral>(TX ctx, TLiteral literal) where TLiteral : ILiteral;
    TO Binary<TOperation>(TX ctx, TI l, TI r)
        where TOperation : IBinaryExpressionOperation<TOperation>;
    TO AddressOf(TX ctx, TI target);
    TO Unary
        <TOperation, TSourceType, TResultType, TOp>
        (TX ctx, TI e)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>;

    TO Conversion<TSource, TTarget>(TX ctx, TI e)
        where TSource : IShaderType
        where TTarget : IShaderType;

    TO VectorSwizzleExtract<TPattern, TElement>(TX ctx, TI e)
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>;


    TO VectorSwizzleReplace<TPattern, TElement>(TX ctx, TI e, TI v)
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>;
    TO VectorComponentExtract<TRank, TVector, TComponent>(TX ctx, TI e)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;

    TO VectorComponentReplace<TRank, TVector, TComponent>(TX ctx, TI e, TI v)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;

    TO StructComponentExtract(TX ctx, MemberDeclaration member, TI e);
    TO StructComponentReplace(TX ctx, MemberDeclaration member, TI e, TI v);
}

public interface IExpression<out T>
{
    TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx);
    IExpression<TR> Select<TR>(Func<T, TR> f);
}

public static class Expression
{
    public static IExpressionSemantic<TX, T, IExpression<T>> Factory<TX, T>()
        => new ExpressionFactorySemantic<TX, T>();
}

