using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

[JsonDerivedType(typeof(LiteralValueExpression), nameof(LiteralValueExpression))]
[JsonDerivedType(typeof(VariableIdentifierExpression), nameof(VariableIdentifierExpression))]
[JsonDerivedType(typeof(FunctionCallExpression), nameof(FunctionCallExpression))]
[JsonDerivedType(typeof(FormalParameterExpression), nameof(FormalParameterExpression))]
[JsonDerivedType(typeof(NamedComponentExpression), nameof(NamedComponentExpression))]
public interface IExpression : IShaderAstNode, ITextDumpable<ILocalDeclarationContext>
{
    IShaderType Type { get; }

    TResult Accept<TResult>(IExpressionVisitor<TResult> visitor);

    IEnumerable<IInstruction> ToInstructions();
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
    T VisitIndirectionExpression(IndirectionExpression expr);
    T VisitAddressOfExpression(AddressOfExpression expr);

    T VisitBinaryExpression
        <TOperation>
        (BinaryOperationExpression<TOperation> expr)
        where TOperation : IBinaryExpressionOperation<TOperation>;

    T VisitUnaryExpression
        <TOperation, TSourceType, TResultType, TOp>
        (UnaryOperationExpression<TOperation> expr)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>;

    T VisitConversionExpression<TTarget>(
        IUnaryExpression expr)
        where TTarget : ISingletonShaderType<TTarget>;

    T VisitVectorSwizzleGetExpression<TPattern, TElement>(IUnaryExpression expr)
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>;

    T VisitVectorComponentGetExpression<TRank, TVector, TComponent>(IUnaryExpression expr)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;

    T VisitFormalParameterExpression(FormalParameterExpression expr);
    T VisitNamedComponentExpression(NamedComponentExpression expr);
}

public static class ExpressionExtension
{
}