using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.IR.Expression;

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
    public IShaderType Type => L.Type;
}
