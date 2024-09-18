using DualDrill.ILSL.IR.Expression;
using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Statement;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssignmentOp
{
    Assign,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulus,
    BitwiseAnd,
    BitwiseOr,
    ExclusiveOr,
    ShiftRight,
    ShiftLeft
}

public sealed record class SimpleAssignmentStatement(
    IExpression L,
    IExpression R,
    AssignmentOp Op
) : IStatement, IForInit, IForUpdate
{
}
