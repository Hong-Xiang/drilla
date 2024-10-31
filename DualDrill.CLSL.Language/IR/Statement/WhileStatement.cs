using DualDrill.CLSL.Language.IR.Expression;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class WhileStatement(
    ImmutableHashSet<IAttribute> Attributes,
    IExpression Expr,
    IStatement Statement
) : IStatement
{
}
