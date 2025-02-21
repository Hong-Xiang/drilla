﻿using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

[JsonDerivedType(typeof(LiteralValueExpression), nameof(LiteralValueExpression))]
[JsonDerivedType(typeof(VariableIdentifierExpression), nameof(VariableIdentifierExpression))]
[JsonDerivedType(typeof(FunctionCallExpression), nameof(FunctionCallExpression))]
[JsonDerivedType(typeof(FormalParameterExpression), nameof(FormalParameterExpression))]
[JsonDerivedType(typeof(VectorSwizzleAccessExpression), nameof(VectorSwizzleAccessExpression))]
[JsonDerivedType(typeof(NamedComponentExpression), nameof(NamedComponentExpression))]
public interface IExpression : IShaderAstNode
{
    IShaderType Type { get; }

    TResult Accept<TResult>(IExpressionVisitor<TResult> visitor);

    IEnumerable<IStructuredStackInstruction> ToInstructions();
    IEnumerable<VariableDeclaration> ReferencedVariables { get; }
}

public sealed class InvalidExpressionTypeException(string message) : Exception(message)
{
}

// either literal or local variable
public interface ICrossBlockExpression : IExpression
{
}

public interface IExpressionVisitor<T>
{
    T VisitLiteralValueExpression(LiteralValueExpression expr);
    T VisitVariableIdentifierExpression(VariableIdentifierExpression expr);
    T VisitFunctionCallExpression(FunctionCallExpression expr);

    T VisitBinaryExpression
        <TOperation, TLeftType, TRightType, TResultType, TOp>
        (IBinaryExpressionOperation<TOperation, TLeftType, TRightType, TResultType, TOp>.Expression expr)
        where TOperation : IBinaryExpressionOperation<TOperation, TLeftType, TRightType, TResultType, TOp>
        where TLeftType : ISingletonShaderType<TLeftType>
        where TRightType : ISingletonShaderType<TRightType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IBinaryOp<TOp>;

    T VisitUnaryExpression
        <TOperation, TSourceType, TResultType, TOp>
        (UnaryExpression<TOperation> expr)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>;

    T VisitFormalParameterExpression(FormalParameterExpression expr);
    T VisitVectorSwizzleAccessExpression(VectorSwizzleAccessExpression expr);
    T VisitNamedComponentExpression(NamedComponentExpression expr);
}

public static class ExpressionExtension
{
}