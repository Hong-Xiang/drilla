using System.Diagnostics;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Statement;

namespace DualDrill.ILSL.IR.Expression;

public static class SyntaxFactory
{
    public static IExpression vec4<TElement>(params IExpression[] arguments)
        where TElement : IScalarType, new()
    {
        Debug.Assert(arguments.Length <= 4);
        var c = VecType<R4, TElement>.Constructors[arguments.Length];
        return Call(c, arguments);
    }
    public static IExpression f32(IExpression expr)
    {
        return Call(FloatType<B32>.Cast, expr);
    }
    public static IExpression i32(IExpression expr)
    {
        return Call(IntType<B32>.Cast, expr);
    }
    public static IExpression Binary(
        IExpression l,
        BinaryArithmeticOp op,
        IExpression r
    ) => new BinaryArithmeticExpression(l, r, op);

    public static IExpression Binary(
        IExpression l,
        BinaryBitwiseOp op,
        IExpression r
    ) => new BinaryBitwiseExpression(l, r, op);



    public static IExpression Identifier(VariableDeclaration variable) => new VariableIdentifierExpression(variable);
    public static IExpression Argument(ParameterDeclaration parameter) => new FormalParameterExpression(parameter);
    public static IExpression Call(FunctionDeclaration callee, params IExpression[] arguments) => new FunctionCallExpression(callee, [.. arguments]);
    public static IExpression Literal(float value) => new LiteralValueExpression(new FloatLiteral<B32>(value));
    public static IExpression Literal(int value) => new LiteralValueExpression(new IntLiteral<B32>(value));
    public static IExpression Literal(uint value) => new LiteralValueExpression(new UIntLiteral<B32>(value));
    public static IStatement Return(IExpression? Expr) => new ReturnStatement(Expr);
    public static IStatement Declare(VariableDeclaration variable) => new VariableOrValueStatement(variable);

}
