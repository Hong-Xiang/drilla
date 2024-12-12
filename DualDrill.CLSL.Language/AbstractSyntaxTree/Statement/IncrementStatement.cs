using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class IncrementStatement(
    IExpression Expr
) : IStatement, IForInit, IForUpdate
{
}
