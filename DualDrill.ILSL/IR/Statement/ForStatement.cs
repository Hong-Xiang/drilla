using DualDrill.ILSL.IR.Expression;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class ForStatement(
    ImmutableHashSet<IShaderAttribute> Attributes,
    ForHeader ForHeader,
    IStatement Statement
) : IStatement
{
}

public sealed record class ForHeader
{
    public IForInit? Init { get; set; } = null;
    public IExpression? Expr { get; set; } = null;
    public IForUpdate? Update { get; set; } = null;
}

