using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class VariableOrValueStatement(VariableDeclaration Variable) : IStatement, IForInit
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitVariableOrValue(this);
}