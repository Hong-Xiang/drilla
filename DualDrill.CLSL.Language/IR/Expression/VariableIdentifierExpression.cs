using DualDrill.CLSL.Language.IR.Declaration;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class VariableIdentifierExpression(VariableDeclaration Variable) : IExpression
{
    public IShaderType Type => Variable.Type;
}
