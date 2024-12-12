using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class VectorAccessExpression(IExpression Base, IExpression Index) : IExpression
{
    public IShaderType Type => ((IVecType)Base.Type).ElementType;
}
