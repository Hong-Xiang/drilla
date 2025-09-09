using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Operation;

public sealed class NopOperation
    : IStatementOperation, ISingleton<NopOperation>
{
    public FunctionDeclaration Function { get; } = new FunctionDeclaration(
        "nop",
        [],
        new FunctionReturn(ShaderType.Unit, []),
        [new ShaderRuntimeMethodAttribute()]);

    public string Name => Function.Name;

    public IInstruction Instruction => throw new NotImplementedException();

    public static NopOperation Instance { get; } = new();

    public IShaderType ResultType => Function.Return.Type;

    private NopOperation() { }

    public IOperationMethodAttribute GetOperationMethodAttribute()
    {
        throw new NotImplementedException();
    }

    public TR Evaluate<TV, TE, TS, TR>(IStatementNode<TV, TE> stmt, TS semantic) where TS : IStatementSemantic<TV, TE, TR>
    {
        throw new NotImplementedException();
    }
}
