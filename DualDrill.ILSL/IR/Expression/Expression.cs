using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Expression;

[JsonDerivedType(typeof(LiteralValueExpression), nameof(LiteralValueExpression))]
[JsonDerivedType(typeof(VariableIdentifierExpression), nameof(VariableIdentifierExpression))]
[JsonDerivedType(typeof(FunctionCallExpression), nameof(FunctionCallExpression))]
[JsonDerivedType(typeof(BinaryArithmeticExpression), nameof(BinaryArithmeticExpression))]
[JsonDerivedType(typeof(BinaryBitwiseExpression), nameof(BinaryBitwiseExpression))]
[JsonDerivedType(typeof(FormalParameterExpression), nameof(FormalParameterExpression))]
[JsonDerivedType(typeof(ParenthesizedExpression), nameof(ParenthesizedExpression))]
[JsonDerivedType(typeof(VectorSwizzleAccessExpression), nameof(VectorSwizzleAccessExpression))]
[JsonDerivedType(typeof(BinaryRelationalExpression), nameof(BinaryRelationalExpression))]
[JsonDerivedType(typeof(BinaryLogicalExpression), nameof(BinaryLogicalExpression))]
[JsonDerivedType(typeof(UnaryLogicalExpression), nameof(UnaryLogicalExpression))]
[JsonDerivedType(typeof(UnaryArithmeticExpression), nameof(UnaryArithmeticExpression))]
[JsonDerivedType(typeof(NamedComponentExpression), nameof(NamedComponentExpression))]
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
    T VisitBinaryRelationalExpression(BinaryRelationalExpression expr);
    T VisitBinaryLogicalExpression(BinaryLogicalExpression expr);
    T VisitUnaryLogicalExpression(UnaryLogicalExpression expr);
    T VisitUnaryArithmeticExpression(UnaryArithmeticExpression expr);
    T VisitFormalParameterExpression(FormalParameterExpression expr);
    T VisitParenthesizedExpression(ParenthesizedExpression expr);
    T VisitVectorSwizzleAccessExpression(VectorSwizzleAccessExpression expr);
    T VisitNamedComponentExpression(NamedComponentExpression expr);
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
            BinaryRelationalExpression e => visitor.VisitBinaryRelationalExpression(e),
            BinaryLogicalExpression e => visitor.VisitBinaryLogicalExpression(e),
            UnaryLogicalExpression e => visitor.VisitUnaryLogicalExpression(e),
            UnaryArithmeticExpression e => visitor.VisitUnaryArithmeticExpression(e),
            LiteralValueExpression e => visitor.VisitLiteralValueExpression(e),
            VariableIdentifierExpression e => visitor.VisitVariableIdentifierExpression(e),
            FormalParameterExpression e => visitor.VisitFormalParameterExpression(e),
            ParenthesizedExpression e => visitor.VisitParenthesizedExpression(e),
            VectorSwizzleAccessExpression e => visitor.VisitVectorSwizzleAccessExpression(e),
            NamedComponentExpression e => visitor.VisitNamedComponentExpression(e),
            _ => throw new NotSupportedException($"Expression Visitor does not support {expr}")
        };
    }
}

interface IConstExpression : IExpression { }
interface IOverrideExpression : IExpression { }
interface IRuntimeExpression : IExpression { }
