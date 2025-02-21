using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public interface IBinaryExpression : IExpression
{
    public IExpression L { get; }
    public IExpression R { get; }
    public IBinaryExpressionOperation Operation { get; }
}