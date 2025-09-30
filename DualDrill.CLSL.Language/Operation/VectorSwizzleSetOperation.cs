using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Instruction;
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
    public IShaderType LeftType => TPattern.Instance.CalleeVecType<TElement>().GetPtrType();
    public IShaderType RightType => TPattern.Instance.ValueVecType<TElement>();

    public static VectorSwizzleSetOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"set.{TPattern.Instance.Name}.{TElement.Instance.Name}";

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.VectorSwizzleSet(inst, this, inst[0], inst[1]);

    public Swizzle.IPattern Pattern => TPattern.Instance;

    public IVecType CalleeVecType => TPattern.Instance.CalleeVecType<TElement>();

    public IVecType ValueVecType => (IVecType)RightType;

    public IScalarType ElementType => TElement.Instance;
}