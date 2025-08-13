using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IVectorSwizzleSetOperation : IBinaryStatementOperation
{
    Swizzle.IPattern Pattern { get; }
    IVecType CalleeVecType { get; }
    IVecType ValueVecType { get; }
    IScalarType ElementType { get; }
}

public sealed class VectorSwizzleSetOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleSetOperation<TPattern, TElement>>
    , IBinaryStatementOperation<VectorSwizzleSetOperation<TPattern, TElement>>
    , IVectorSwizzleSetOperation
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public static VectorSwizzleSetOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"set.{TPattern.Instance.Name}.{TElement.Instance.Name}";

    public IShaderType LeftType => TPattern.Instance.CalleeVecType<TElement>().GetPtrType();
    public IShaderType RightType => TPattern.Instance.ValueVecType<TElement>();

    public Swizzle.IPattern Pattern => TPattern.Instance;

    public IVecType CalleeVecType => TPattern.Instance.CalleeVecType<TElement>();

    public IVecType ValueVecType => (IVecType)RightType;

    public IScalarType ElementType => TElement.Instance;

    public IStatement CreateStatement(IExpression target, IExpression value)
        => TPattern.Instance.CreateSwizzleSetStatement<TElement>(target, value);
}