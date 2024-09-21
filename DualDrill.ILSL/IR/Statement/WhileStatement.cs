using DualDrill.ILSL.IR.Expression;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class WhileStatement(
    ImmutableHashSet<IAttribute> Attributes,
    IExpression Expr, 
    IStatement Statement
) : IStatement
{
}
