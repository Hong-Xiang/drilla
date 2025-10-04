using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Operation.Pointer;

public sealed class AccessChainOperation : IOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => "OpAccessChain";

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic) where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO>
    {
        throw new NotImplementedException();
    }

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }
}

