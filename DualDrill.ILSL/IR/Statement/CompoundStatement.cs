using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class CompoundStatement(ImmutableArray<IStatement> Statements) : IStatement
{
}



