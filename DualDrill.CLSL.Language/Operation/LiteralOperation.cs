using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Operation;

public sealed class LiteralOperation : IOperation
{
    public FunctionDeclaration Function => throw new NotImplementedException();

    public string Name => "literal";


    public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.Literal(inst, this, inst.Result, inst[0]);
}