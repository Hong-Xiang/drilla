using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public interface IVariableIdentifierResolveResult
{
    IShaderType Type { get; }
    string Name { get; }
}

public sealed record class VariableIdentifierExpression(IVariableIdentifierResolveResult Variable) : IExpression
{
    public IShaderType Type => Variable.Type;
}
