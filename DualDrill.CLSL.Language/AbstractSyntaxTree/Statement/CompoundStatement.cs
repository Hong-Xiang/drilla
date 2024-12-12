using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class CompoundStatement(ImmutableArray<IStatement> Statements) : IStatement
{
}



