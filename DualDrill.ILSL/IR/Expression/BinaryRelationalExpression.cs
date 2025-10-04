using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BinaryRelationalOp
{
    LessThan,
    GreaterThan,
    LessThanEqual,
    GreaterThanEqual,
    Equal,
    NotEqual
}

public sealed record class BinaryRelationalExpression(
    IExpression L,
    IExpression R,
    BinaryRelationalOp Op
) : IExpression
{
}
