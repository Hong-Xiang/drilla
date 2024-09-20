using DualDrill.ILSL.IR.Expression;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class IncrementStatement(
    IExpression Expr
) : IStatement, IForInit, IForUpdate
{
}
