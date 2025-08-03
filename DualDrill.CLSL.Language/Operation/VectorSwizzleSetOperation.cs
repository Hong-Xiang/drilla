using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.ValueInstruction;

namespace DualDrill.CLSL.Language.Operation;

public sealed class VectorSwizzleSetOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleSetOperation<TPattern, TElement>>
    , IBinaryStatementOperation<VectorSwizzleSetOperation<TPattern, TElement>>
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public static VectorSwizzleSetOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"set.{TPattern.Instance.Name}.{TElement.Instance.Name}";

    public IShaderType LeftType => TPattern.Instance.SourceType<TElement>().GetPtrType();
    public IShaderType RightType => TPattern.Instance.TargetType<TElement>();

    public IStatement CreateStatement(IExpression target, IExpression value)
        => TPattern.Instance.CreateSwizzleSetStatement<TElement>(target, value);

    public IStatementOperationValueInstruction ToValueInstruction(IValue l, IValue r)
    {
        throw new NotImplementedException();
    }
}