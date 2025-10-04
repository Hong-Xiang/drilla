using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.Common.Nat;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.IR.Expression;

public static class SyntaxFactory
{
    //public static IExpression vec4<TElement>(params IExpression[] arguments)
    //    where TElement : IScalarType<TElement>
    //{
    //    Debug.Assert(arguments.Length <= 4);
    //    var c = VecType<N4, TElement>.Constructors[arguments.Length];
    //    return Call(c, arguments);
    //}
    //public static IExpression vec3<TElement>(params IExpression[] arguments)
    //    where TElement : IScalarType<TElement>
    //{
    //    Debug.Assert(arguments.Length <= 3);
    //    var c = VecType<N3, TElement>.Constructors[arguments.Length];
    //    return Call(c, arguments);
    //}
    //public static IExpression vec2<TElement>(params IExpression[] arguments)
    //where TElement : IScalarType<TElement>
    //{
    //    Debug.Assert(arguments.Length <= 2);
    //    var c = VecType<N2, TElement>.Constructors[arguments.Length];
    //    return Call(c, arguments);
    //}
    //public static IExpression f32(IExpression expr)
    //{
    //    return Call(FloatType<N32>.Cast, expr);
    //}
    //public static IExpression i32(IExpression expr)
    //{
    //    return Call(IntType<N32>.Cast, expr);
    //}
    //public static IExpression Binary(
    //    IExpression l,
    //    BinaryArithmeticOp op,
    //    IExpression r
    //) => new BinaryArithmeticExpression(l, r, op);

    //public static IExpression Binary(
    //    IExpression l,
    //    BinaryBitwiseOp op,
    //    IExpression r
    //) => new BinaryBitwiseExpression(l, r, op);



    public static IExpression Identifier(VariableDeclaration variable) => new VariableIdentifierExpression(variable);
    public static IExpression Argument(ParameterDeclaration parameter) => new FormalParameterExpression(parameter);
    public static IExpression Call(FunctionDeclaration callee, params IExpression[] arguments) => new FunctionCallExpression(callee, [.. arguments]);
    public static IExpression Literal(float value) => new LiteralValueExpression(new FloatLiteral(N32.Instance, value));
    public static IExpression Literal(int value) => new LiteralValueExpression(new IntLiteral(N32.Instance, value));
    public static IExpression Literal(uint value) => new LiteralValueExpression(new UIntLiteral(N32.Instance, value));
    public static IExpression Literal(bool value) => new LiteralValueExpression(new BoolLiteral(value));
    public static IStatement Return(IExpression? Expr) => new ReturnStatement(Expr);
    public static IStatement Declare(VariableDeclaration variable) => new VariableOrValueStatement(variable);
    public static IStatement IfElse(IExpression expr, CompoundStatement ifBranch, CompoundStatement elseBranch) => new IfStatement(
        Attributes: [],
        new IfClause(expr, ifBranch),
        ElseIfClause: []
    )
    {
        Else = elseBranch
    };

}
