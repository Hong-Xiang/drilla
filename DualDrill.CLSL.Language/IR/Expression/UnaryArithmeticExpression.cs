using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.IR.Expression;

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
