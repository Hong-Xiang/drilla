using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.IR.Expression;

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
    public IShaderType Type => ShaderType.Bool;
}
