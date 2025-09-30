using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class CallOperation(FunctionType calleeType) : IOperation
{
    public IShaderType ResultType => calleeType.ResultType;
    public FunctionType CalleeType => calleeType;

    public string Name => "call";

    public FunctionDeclaration Function => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.Call(inst, this, inst.Result, inst[0], [.. inst.Operands.ToImmutableArray()[1..]]);

    public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotImplementedException();
}