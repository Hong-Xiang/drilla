using DualDrill.ILSL.IR.Expression;

namespace DualDrill.ILSL.IR.Statement;

public sealed record class ReturnStatement(IExpression? Expr) : IStatement
{
}



