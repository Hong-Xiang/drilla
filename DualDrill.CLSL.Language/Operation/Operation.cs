using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.Operation;

public static class Operation
{
    public static IExpression CreateExpression(this IUnaryExpressionOperation operation, IExpression expr)
        => operation.CreateExpression(expr);
}