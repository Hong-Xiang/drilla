using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorComponentGetOperation
{
    Swizzle.IComponent Component { get; }
}

// TODO: change math code gen to use <TRank, TElement, TComponent> generic parameters
public sealed class VectorComponentGetExpressionOperation<TRank, TVector, TComponent>
    : IUnaryExpressionOperation<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>
    , IVectorComponentGetOperation
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentGetExpressionOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public string Name => $"get.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    public IShaderType SourceType => TVector.Instance.GetPtrType();
    public IShaderType ResultType => TVector.Instance.ElementType;




    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context) =>
        TVector.Instance.Accept(new SizedVecVisitor<TX, TR>(semantic, context));

    public Swizzle.IComponent Component => TComponent.Instance;

    private sealed class SizedVecVisitor<TX, TR>(
        IUnaryExpressionOperationSemantic<TX, TR> semantic,
        TX context
    ) : ISizedVecType<TRank, TVector>.ISizedVisitor<TR>
    {
        public TR Visit<TElement>(VecType<TRank, TElement> t) where TElement : IScalarType<TElement> =>
            semantic.VectorComponentGet<TRank, TElement, TComponent>(context);
    }
}