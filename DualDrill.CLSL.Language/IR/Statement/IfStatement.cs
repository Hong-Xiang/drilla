using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class IfStatement(
    IExpression Expr,
    CompoundStatement TrueBody,
    CompoundStatement FalseBody,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IStatement
{
}

