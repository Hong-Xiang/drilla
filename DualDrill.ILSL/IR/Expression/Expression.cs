using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonDerivedType(typeof(LiteralValueExpression), nameof(LiteralValueExpression))]
[JsonDerivedType(typeof(VariableIdentifierExpression), nameof(VariableIdentifierExpression))]
[JsonDerivedType(typeof(FunctionCallExpression), nameof(FunctionCallExpression))]
[JsonDerivedType(typeof(BinaryArithmeticExpression), nameof(BinaryArithmeticExpression))]
[JsonDerivedType(typeof(BinaryBitwiseExpression), nameof(BinaryBitwiseExpression))]
[JsonDerivedType(typeof(FormalParameterExpression), nameof(FormalParameterExpression))]
[JsonDerivedType(typeof(ParenthesizedExpression), nameof(ParenthesizedExpression))]
public interface IExpression : INode
{
}

public interface IExpressionVisitor<T>
{
    T VisitLiteralValueExpression(LiteralValueExpression expr);
    T VisitVariableIdentifierExpression(VariableIdentifierExpression expr);
    T VisitFunctionCallExpression(FunctionCallExpression expr);
    T VisitBinaryArithmeticExpression(BinaryArithmeticExpression expr);
    T VisitBinaryBitwiseExpression(BinaryBitwiseExpression expr);
    T VisitFormalParameterExpression(FormalParameterExpression expr);
    T VisitParenthesizedExpression(ParenthesizedExpression expr);
}

public static class ExpressionExtension
{
    public static T AcceptVisitor<T>(this IExpression expr, IExpressionVisitor<T> visitor)
    {
        return expr switch
        {
            FunctionCallExpression e => visitor.VisitFunctionCallExpression(e),
            BinaryArithmeticExpression e => visitor.VisitBinaryArithmeticExpression(e),
            BinaryBitwiseExpression e => visitor.VisitBinaryBitwiseExpression(e),
            LiteralValueExpression e => visitor.VisitLiteralValueExpression(e),
            VariableIdentifierExpression e => visitor.VisitVariableIdentifierExpression(e),
            FormalParameterExpression e => visitor.VisitFormalParameterExpression(e),
            ParenthesizedExpression e => visitor.VisitParenthesizedExpression(e),
            _ => throw new NotSupportedException($"Expression Visitor does not support {expr}")
        };
    }
}

interface IConstExpression : IExpression { }
interface IOverrideExpression : IExpression { }
interface IRuntimeExpression : IExpression { }
