using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorSwizzleGetOperation
{
    Swizzle.IPattern Pattern { get; }
}

public sealed class VectorSwizzleGetExpressionOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleGetExpressionOperation<TPattern, TElement>>
    , IUnaryExpressionOperation<VectorSwizzleGetExpressionOperation<TPattern, TElement>>
    , IVectorSwizzleGetOperation
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public IShaderType SourceType => TPattern.Instance.CalleeVecType<TElement>().GetPtrType();
    public IShaderType ResultType => TPattern.Instance.ValueVecType<TElement>();


    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context) =>
        TPattern.Instance.Evaluate(new UnwrappPattern<TX, TR>(semantic, context));

    public static VectorSwizzleGetExpressionOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"get.{TPattern.Instance.Name}.{TElement.Instance.Name}";


    public Swizzle.IPattern Pattern => TPattern.Instance;

    public override string ToString() => Name;

    private sealed class UnwrappPattern<TX, TR>(
        IUnaryExpressionOperationSemantic<TX, TR> semantic,
        TX context
    ) : Swizzle.IPatternSemantic<TR>
    {
        public TR Pattern<TRank, TSP>()
            where TRank : IRank<TRank>
            where TSP : Swizzle.ISizedPattern<TRank, TSP> =>
            semantic.VectorSwizzleGet<TRank, TElement, TSP>(context);
    }
}