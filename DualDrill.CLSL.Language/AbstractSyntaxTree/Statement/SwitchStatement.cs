using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class SwitchStatement(
    IExpression Expr,
    ImmutableHashSet<SwitchCase> Cases,
    CompoundStatement DefaultCase
) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitSwitch(this);
}

public sealed record class SwitchCase(
    LiteralValueExpression Label,
    CompoundStatement Body
)
{
}
