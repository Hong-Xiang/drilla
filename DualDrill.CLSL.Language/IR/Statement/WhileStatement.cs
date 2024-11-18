using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class WhileStatement(
    ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes,
    IExpression Expr,
    IStatement Statement
) : IStatement
{
}
