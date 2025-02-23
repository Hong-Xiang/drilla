namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class LoopStatement(CompoundStatement Body) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitLoop(this);
}