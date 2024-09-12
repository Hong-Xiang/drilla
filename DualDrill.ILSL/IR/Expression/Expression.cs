namespace DualDrill.ILSL.IR.Expression;

public interface IExpression
{
}

public interface IExpressionVisitor<T>
{
    T VisitLiteralValueExpression(LiteralValueExpression expr);
    T VisitVariableIdentifierExpression(VariableIdentifierExpression expr);
    T VisitFunctionCallExpression(FunctionCallExpression expr);
    T VisitBinaryArithmeticExpression(BinaryArithmeticExpression expr);
}

public static class ExpressionExtension
{
    public static T AcceptVisitor<T>(this IExpression expr, IExpressionVisitor<T> visitor)
    {
        return expr switch
        {
            FunctionCallExpression e => visitor.VisitFunctionCallExpression(e),
            BinaryArithmeticExpression e => visitor.VisitBinaryArithmeticExpression(e),
            LiteralValueExpression e => visitor.VisitLiteralValueExpression(e),
            VariableIdentifierExpression e => visitor.VisitVariableIdentifierExpression(e),
            _ => throw new NotSupportedException($"Expression Visitor does not support {expr}")
        };
    }
}

interface IConstExpression : IExpression { }
interface IOverrideExpression : IExpression { }
interface IRuntimeExpression : IExpression { }
