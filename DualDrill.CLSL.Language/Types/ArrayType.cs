using DualDrill.CLSL.Language.IR.Expression;

namespace DualDrill.CLSL.Language.Types;

public sealed record class ArrayType(
    IShaderType ElementType,
    IExpression Length
)
{
}
