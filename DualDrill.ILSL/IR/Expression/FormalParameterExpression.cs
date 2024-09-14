using DualDrill.ILSL.IR.Declaration;

namespace DualDrill.ILSL.IR.Expression;

public sealed record class FormalParameterExpression(ParameterDeclaration Parameter) : IExpression
{
}
