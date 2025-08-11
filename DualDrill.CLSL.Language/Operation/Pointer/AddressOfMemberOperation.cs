using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation.Pointer;

public sealed class AddressOfMemberOperation(MemberDeclaration Member)
    : IAccessChainOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();

    public IInstruction Instruction => throw new NotImplementedException();

    public IShaderType SourceType => throw new NotImplementedException();

    public IShaderType ResultType => Member.Type.GetPtrType();

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
