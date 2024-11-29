using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public interface IVariableIdentifierResolveResult
{
}

public sealed record class VariableIdentifierExpression(IVariableIdentifierResolveResult Variable) : IExpression
{
    public IShaderType Type => Variable switch
    {
        VariableDeclaration v => v.Type,
        _ => throw new NotImplementedException()
    };
}
