using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class IfStatement(
    IExpression Expr,
    CompoundStatement TrueBody,
    CompoundStatement FalseBody,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitIf(this);
}