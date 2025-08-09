using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Expression;

sealed class ExpressionFactorySemantic<TX, T> : IExpressionSemantic<TX, T, IExpression<T>>
{
    public IExpression<T> AddressOf(TX ctx, T target)
        => new AddressOfExpression<T>(target);

    public IExpression<T> Binary<TOperation>(TX ctx, T l, T r)
        where TOperation : IBinaryExpressionOperation<TOperation>
        => new BinaryExpression<TOperation, T>(l, r);

    public IExpression<T> Conversion<TSource, TTarget>(TX ctx, T e)
        where TSource : IShaderType
        where TTarget : IShaderType
    {
        throw new NotImplementedException();
    }


    public IExpression<T> Literal<TLiteral>(TX ctx, TLiteral literal)
        where TLiteral : ILiteral
        => new LiteralExpression<TLiteral, T>(literal);

    public IExpression<T> StructComponentExtract(TX ctx, MemberDeclaration member, T e)
    {
        throw new NotImplementedException();
    }

    public IExpression<T> StructComponentReplace(TX ctx, MemberDeclaration member, T e, T v)
    {
        throw new NotImplementedException();
    }

    public IExpression<T> Unary<TOperation, TSourceType, TResultType, TOp>(TX ctx, T e)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>
    {
        throw new NotImplementedException();
    }

    public IExpression<T> VectorComponentExtract<TRank, TVector, TComponent>(TX ctx, T e)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
    {
        throw new NotImplementedException();
    }

    public IExpression<T> VectorComponentReplace<TRank, TVector, TComponent>(TX ctx, T e, T v)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
    {
        throw new NotImplementedException();
    }

    public IExpression<T> VectorSwizzleExtract<TPattern, TElement>(TX ctx, T e)
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>
    {
        throw new NotImplementedException();
    }

    public IExpression<T> VectorSwizzleReplace<TPattern, TElement>(TX ctx, T e, T v)
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>
    {
        throw new NotImplementedException();
    }
}

