using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Symbol;

public readonly record struct ShaderValueDeclaration(ShaderValue Value, IShaderType Type)
{
    public IExpressionTree<ShaderValue> ValueExpr => ExpressionTree.Value(Value);
}