using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.IR.Expression;

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
    public IShaderType Type => ShaderType.Bool;
}
