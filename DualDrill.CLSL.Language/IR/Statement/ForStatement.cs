using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class ForStatement(
    ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes,
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

