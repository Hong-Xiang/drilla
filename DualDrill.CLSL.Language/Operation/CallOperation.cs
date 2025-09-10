using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class CallOperation(FunctionType calleeType) : IOperation
{
    public IShaderType ResultType => calleeType.ResultType;
    public FunctionType CalleeType => calleeType;

    public string Name => "call";

    public IInstruction Instruction => throw new NotImplementedException();

    public FunctionDeclaration Function => throw new NotImplementedException();

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }
}
