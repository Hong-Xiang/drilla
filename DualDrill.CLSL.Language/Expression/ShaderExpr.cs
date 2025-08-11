using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Expression;

public readonly struct ShaderExpr(IExpressionTree<ShaderValue> Value)
{
    public static ShaderExpr Value(ShaderValue value)
        => new(new LeafExpression<ShaderValue>(value));
}
