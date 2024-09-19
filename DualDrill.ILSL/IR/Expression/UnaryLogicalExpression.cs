using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnaryLogicalOp
{
    Not
}

public sealed record class UnaryLogicalExpression(
    IExpression Expr,
    UnaryLogicalOp Op
) : IExpression
{
}
