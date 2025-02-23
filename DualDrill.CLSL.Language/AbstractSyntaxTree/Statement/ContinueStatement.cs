namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed class ContinueStatement : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitContinue(this);
}
