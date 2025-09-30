using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Operation;

public sealed class StoreOperation : IOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => "store";


    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.Store(inst, this, inst[0], inst[1]);

    public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotImplementedException();
}