namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class BreakStatement : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitBreak(this);
}