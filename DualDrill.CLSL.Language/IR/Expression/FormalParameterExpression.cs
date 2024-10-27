using DualDrill.CLSL.Language.IR.Declaration;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class FormalParameterExpression(ParameterDeclaration Parameter) : IExpression
{
}
