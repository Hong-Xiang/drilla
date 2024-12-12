using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class WhileStatement(
    ImmutableHashSet<IShaderAttribute> Attributes,
    IExpression Expr,
    IStatement Statement
) : IStatement
{
}
