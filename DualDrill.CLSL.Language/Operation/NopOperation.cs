using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public sealed class NopOperation
    : IOperation, ISingleton<NopOperation>
{
    private NopOperation()
    {
    }

    public IShaderType ResultType => Function.Return.Type;

    public FunctionDeclaration Function { get; } = new(
        "nop",
        [],
        new FunctionReturn(ShaderType.Unit, []),
        [new ShaderRuntimeMethodAttribute()]);

    public string Name => Function.Name;


    public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotImplementedException();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction2<TV, TR> inst, TS semantic)
        where TS : IOperationSemantic<Instruction2<TV, TR>, TV, TR, TO> =>
        semantic.Nop(inst, this);

    public static NopOperation Instance { get; } = new();
}