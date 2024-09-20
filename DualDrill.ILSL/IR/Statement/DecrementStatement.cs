using DualDrill.ILSL.IR.Expression;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class DecrementStatement(
    IExpression Expr
) : IStatement, IForInit, IForUpdate
{
}
