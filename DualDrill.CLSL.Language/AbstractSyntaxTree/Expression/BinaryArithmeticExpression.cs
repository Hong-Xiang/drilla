using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class BinaryArithmeticExpression(
    IExpression L,
    IExpression R,
    BinaryArithmetic.Op Op
) : IExpression
{
    public IShaderType Type => L.Type;
}
