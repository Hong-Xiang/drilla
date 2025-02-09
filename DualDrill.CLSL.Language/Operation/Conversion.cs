using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public interface IScalarConversionOperation
{
    IStructuredStackInstruction Instruction { get; }
}

public sealed class ScalarConversionOperation<TSource, TTarget>
    : IScalarConversionOperation, IUnaryOperation<ScalarConversionOperation<TSource, TTarget>>
    where TSource : IScalarType<TSource>
    where TTarget : IScalarType<TTarget>
{
    public static ScalarConversionOperation<TSource, TTarget> Instance { get; } = new();

    public IStructuredStackInstruction Instruction =>
        UnaryOperationInstruction<ScalarConversionOperation<TSource, TTarget>>.Instance;

    public IShaderType SourceType => TSource.Instance;

    public IShaderType ResultType => TTarget.Instance;

    public FunctionDeclaration Function { get; } = new(
        $"{TTarget.Instance.Name}",
        [new ParameterDeclaration("v", TSource.Instance, [])],
        new FunctionReturn(TTarget.Instance, []),
        [new ShaderRuntimeMethodAttribute()]
    );

    public string Name => $"conv.{TSource.Instance.Name}->{TTarget.Instance.Name}";
    
    public override string ToString() => Name;

    public IExpression CreateExpression(IExpression expr)
        => new FunctionCallExpression(Function, [expr]);
}