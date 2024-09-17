using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Statement;

// attribute* if_clause else_if_clause* else_clause?
public sealed record class IfStatement(
    ImmutableHashSet<IAttribute> Attributes,
    IfClause IfClause,
    ImmutableArray<IfClause> ElseIfClause
) : IStatement
{
    public CompoundStatement? Else { get; set; } = null;
}

public readonly record struct IfClause(IExpression Expr, CompoundStatement CompountStatement)
{
}
