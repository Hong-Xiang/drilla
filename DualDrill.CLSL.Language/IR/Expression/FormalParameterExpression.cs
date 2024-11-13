using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class FormalParameterExpression(ParameterDeclaration Parameter) : IExpression
{
    public IShaderType Type => Parameter.Type;
}
