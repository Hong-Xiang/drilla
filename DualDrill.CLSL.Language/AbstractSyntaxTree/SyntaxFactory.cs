using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree;

public static class SyntaxFactory
{
    public static IExpression Call(FunctionDeclaration callee, params IExpression[] arguments) =>
        new FunctionCallExpression(callee, [.. arguments]);

    public static LiteralValueExpression Literal(float value) => new(new F32Literal(value));
    public static LiteralValueExpression Literal(double value) => new(new F64Literal(value));
    public static LiteralValueExpression Literal(int value) => new(new I32Literal(value));
    public static LiteralValueExpression Literal(uint value) => new(new U32Literal(value));
    public static LiteralValueExpression Literal(bool value) => new(new BoolLiteral(value));

    public static LiteralValueExpression Literal<TLiteral>(TLiteral literal)
        where TLiteral : ILiteral
        => new(literal);

    public static VariableIdentifierExpression VarIdentifier(VariableDeclaration variable) => new(variable);
    public static FormalParameterExpression ArgIdentifier(ParameterDeclaration parameter) => new(parameter);

    public static AddressOfExpression AddressOf(IExpression expr) => new(expr);
    public static IndirectionExpression Indirection(IExpression expr) => new(expr);

    public static ReturnStatement Return(IExpression? Expr) => new(Expr);
    public static BreakStatement Break() => new();
    public static ContinueStatement Continue() => new();
    public static VariableOrValueStatement VarDeclaration(VariableDeclaration variable) => new(variable);

    public static NamedComponentExpression FieldIdentifier(IExpression target, MemberDeclaration member) =>
        new(target, member);

    public static IfStatement If(IExpression expr, CompoundStatement trueBody, CompoundStatement falseBody) =>
        new(expr, trueBody, falseBody, []);

    public static SimpleAssignmentStatement AssignStatement(
        IExpression target,
        IExpression value
    ) => new(target, value, AssignmentOp.Assign);

    public static CompoundStatement CompoundStatement(
        params ReadOnlySpan<IStatement> statements
    ) => new([.. statements]);

    public static PhonyAssignmentStatement ExpressionStatement(IExpression expression) =>
        new(expression);

    public static IExpression Not(IExpression e)
    {
        Debug.Assert(e.Type is BoolType);
        return new UnaryLogicalExpression(e, UnaryLogicalOp.Not);
    }
}