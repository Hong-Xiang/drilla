using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR.Expression;

public sealed record class IndexingExpression(IExpression Base, IExpression Index) : IExpression
{
    public IShaderType Type => Base.Type switch
    {
        _ => throw new InvalidExpressionTypeException(nameof(IndexingExpression))
    };
}
