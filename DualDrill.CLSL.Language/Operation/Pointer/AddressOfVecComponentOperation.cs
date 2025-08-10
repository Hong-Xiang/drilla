using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation.Pointer;

public sealed record class AddressOfVecComponentOperation(
    IVecType Target,
    Swizzle.IComponent Component) : IAddressOfChainOperation
{
    public IShaderType SourceType => Target.GetPtrType();

    public IShaderType ResultType => Target.ElementType.GetPtrType();

    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => $"{Target.Name}.set.{Component.Name}";

    public IInstruction Instruction => throw new NotImplementedException();

    public IUnaryExpression CreateExpression(IExpression expr)
    {
        throw new NotImplementedException();
    }

    public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context)
    {
        throw new NotImplementedException();
    }

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }
}
