using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnaryArithmeticOp
{
    Minus
}

public sealed record class UnaryArithmeticExpression(
    IExpression Expr,
    UnaryArithmeticOp Op
) : IExpression
{
}
