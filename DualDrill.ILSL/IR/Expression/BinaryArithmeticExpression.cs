using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BinaryArithmeticOp
{
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Remainder
}

public sealed record class BinaryArithmeticExpression(
    IExpression L,
    IExpression R,
    BinaryArithmeticOp Op
) : IExpression
{
}
