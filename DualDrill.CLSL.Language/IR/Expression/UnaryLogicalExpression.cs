using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.IR.Expression;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnaryLogicalOp
{
    Not
}

public sealed record class UnaryLogicalExpression(
    IExpression Expr,
    UnaryLogicalOp Op
) : IExpression
{
    public IShaderType Type => Expr.Type;
}
