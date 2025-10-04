using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class ZeroConstructorOperation(IShaderType type) : IOperation
{
    public IShaderType ResultType => type;
    public FunctionDeclaration Function => type switch
    {
        IVecType vt => vt.ZeroConstructor,
        _ => throw new NotSupportedException()
    };

    public string Name => $"ctor.zero.{type.Name}";

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic) where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO>
        => semantic.ZeroConstructorOperation(inst, this, inst.Result ?? throw new ArgumentException("result should not be null"));

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }
}
