using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

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
}