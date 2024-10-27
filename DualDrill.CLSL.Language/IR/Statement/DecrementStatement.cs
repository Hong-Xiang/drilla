using DualDrill.CLSL.Language.IR.Expression;

namespace DualDrill.CLSL.Language.IR.Statement;

public sealed record class DecrementStatement(
    IExpression Expr
) : IStatement, IForInit, IForUpdate
{
}
