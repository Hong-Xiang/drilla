using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

// TODO: change math code gen to use <TRank, TElement, TComponent> generic parameters
public sealed class VectorComponentGetExpressionOperation<TRank, TVector, TComponent>
    : IUnaryExpressionOperation<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentGetExpressionOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public string Name => $"get.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    public IShaderType SourceType => TVector.Instance.GetPtrType();
    public IShaderType ResultType => TVector.Instance.ElementType;

    IUnaryExpression IUnaryExpressionOperation.CreateExpression(IExpression expr)
        => new UnaryOperationExpression<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>(expr);

    public IInstruction Instruction =>
        UnaryExpressionOperationInstruction<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>>.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<VectorComponentGetExpressionOperation<TRank, TVector, TComponent>> expr)
        => visitor.VisitVectorComponentGetExpression<TRank, TVector, TComponent>(expr);

    sealed class SizedVecVisitor<TX, TR>(
        IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context
    ) : ISizedVecType<TRank, TVector>.ISizedVisitor<TR>
    {
        public TR Visit<TElement>(VecType<TRank, TElement> t) where TElement : IScalarType<TElement>
            => semantic.VectorComponentGet<TRank, TElement, TComponent>(context);
    }

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context)
        => TVector.Instance.Accept(new SizedVecVisitor<TX, TR>(semantic, context));
}