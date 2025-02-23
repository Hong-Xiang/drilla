using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public sealed class VectorComponentGetOperation<TRank, TVector, TComponent>
    : IUnaryOperation<VectorComponentGetOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentGetOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public string Name => $"get.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    public IShaderType SourceType => TVector.Instance.GetPtrType();
    public IShaderType ResultType => TVector.Instance.ElementType;

    IUnaryExpression IUnaryExpressionOperation.CreateExpression(IExpression expr)
        => new UnaryOperationExpression<VectorComponentGetOperation<TRank, TVector, TComponent>>(expr);


    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryOperationExpression<VectorComponentGetOperation<TRank, TVector, TComponent>> expr)
        => visitor.VisitVectorComponentGetExpression<TRank, TVector, TComponent>(expr);
}