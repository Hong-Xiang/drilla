using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

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
