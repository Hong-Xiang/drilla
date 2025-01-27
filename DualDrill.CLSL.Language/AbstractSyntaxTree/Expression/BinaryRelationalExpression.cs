using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;


public sealed record class BinaryRelationalExpression(
    IExpression L,
    IExpression R,
    BinaryRelation.OpKind Op
) : IExpression
{
    public IShaderType Type => ShaderType.Bool;
}
