using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

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

