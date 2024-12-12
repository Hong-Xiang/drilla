using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

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
    public IShaderType Type => L.Type;
}
