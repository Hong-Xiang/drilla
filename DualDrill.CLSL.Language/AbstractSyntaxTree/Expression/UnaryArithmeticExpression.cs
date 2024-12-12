using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

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
    public IShaderType Type => Expr.Type;
}
