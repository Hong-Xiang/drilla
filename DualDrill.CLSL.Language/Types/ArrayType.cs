using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

namespace DualDrill.CLSL.Language.Types;

public sealed record class ArrayType(
    IShaderType ElementType,
    IExpression Length
)
{
}
