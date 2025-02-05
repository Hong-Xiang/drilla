using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class BinaryExpression<TOperation>(
    IExpression Left,
    IExpression Right
) : IExpression
    where TOperation : IBinaryOperation<TOperation>
{
    public IShaderType Type => TOperation.Instance.ResultType;

    TResult IExpression.Accept<TResult>(IExpressionVisitor<TResult> visitor)
    {
        return visitor.VisitBinaryExpression<TOperation>(this);
    }
}
