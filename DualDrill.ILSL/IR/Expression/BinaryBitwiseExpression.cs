using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BinaryBitwiseOp
{
    BitwiseOr,
    BitwiseAnd,
    BitwiseExclusiveOr
}

public sealed record class BinaryBitwiseExpression(
    IExpression L,
    IExpression R,
    BinaryBitwiseOp Op
) : IExpression
{
}
