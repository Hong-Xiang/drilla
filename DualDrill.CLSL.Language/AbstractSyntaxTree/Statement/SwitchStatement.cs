using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class SwitchStatement(
    IExpression Expr,
    ImmutableHashSet<SwitchCase> Cases,
    CompoundStatement DefaultCase
) : IStatement
{
}

public sealed record class SwitchCase(
    LiteralValueExpression Label,
    CompoundStatement Body
)
{
}
