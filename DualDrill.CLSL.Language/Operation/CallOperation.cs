using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Operation;

public sealed class CallOperation(FunctionType calleeType) : IOperation
{
    public IShaderType ResultType => calleeType.ResultType;
    public FunctionType CalleeType => calleeType;

    public string Name => "call";

    public IInstruction Instruction => throw new NotImplementedException();

    public FunctionDeclaration Function => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction2<TV, TR> inst, TS semantic) where TS : IOperationSemantic<Instruction2<TV, TR>, TV, TR, TO>
        => semantic.Call(inst, this, inst.Result, inst[0], [.. inst.Operands.ToImmutableArray()[1..]]);

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }
}
