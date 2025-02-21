using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.Operation;

public static class Operation
{
    public static IExpression CreateExpression(this IUnaryExpressionOperation operation, IExpression expr)
        => operation.CreateExpression(expr);

    public static int Foo(uint x)
    {
        if (x == uint.MaxValue)
        {
            return 1;
        }
        else if (x == 0)
        {
            return 2;
        }
        else
        {
            return 3;
        }
    }

 

    public static uint Bar()
    {
        return uint.MaxValue;
    }
}