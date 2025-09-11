using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Operation;

public sealed class LoadOperation : IOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => "load";

    public IInstruction Instruction => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction2<TV, TR> inst, TS semantic) where TS : IOperationSemantic<Instruction2<TV, TR>, TV, TR, TO>
        => semantic.Load(inst, this, inst.Result, inst[0]);

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }
}
