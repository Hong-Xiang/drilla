using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BinaryLogicalOp
{
    And,
    Or
}

public sealed record class BinaryLogicalExpression(
    IExpression L,
    IExpression R,
    BinaryLogicalOp Op
) : IExpression
{
}
