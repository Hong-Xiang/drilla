using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class IndexingExpression(IExpression Base, IExpression Index) : IExpression
{
    public IShaderType Type => Base.Type switch
    {
        IVecType vecType => vecType.ElementType,
        _ => throw new InvalidExpressionTypeException(nameof(IndexingExpression))
    };
}
