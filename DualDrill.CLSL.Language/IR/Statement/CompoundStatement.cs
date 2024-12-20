﻿using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class CompoundStatement(ImmutableArray<IStatement> Statements) : IStatement
{
}



