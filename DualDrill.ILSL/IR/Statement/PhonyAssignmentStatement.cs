using DualDrill.ILSL.IR.Expression;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class PhonyAssignmentStatement(
    IExpression Expr
) : IStatement, IForInit, IForUpdate
{
}