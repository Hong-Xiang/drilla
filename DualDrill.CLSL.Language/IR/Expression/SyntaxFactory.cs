using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.Common.Nat;

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



    public static IExpression Call(FunctionDeclaration callee, params IExpression[] arguments) => new FunctionCallExpression(callee, [.. arguments]);
    public static LiteralValueExpression Literal(float value) => new(new FloatLiteral(N32.Instance, value));
    public static LiteralValueExpression Literal(double value) => new(new FloatLiteral(N64.Instance, value));
    public static LiteralValueExpression Literal(int value) => new(new IntLiteral(N32.Instance, value));
    public static LiteralValueExpression Literal(uint value) => new(new UIntLiteral(N32.Instance, value));
    public static LiteralValueExpression Literal(bool value) => new(new BoolLiteral(value));

    public static VariableIdentifierExpression VarIdentifier(VariableDeclaration variable) => new(variable);
    public static FormalParameterExpression ArgIdentifier(ParameterDeclaration parameter) => new(parameter);

    public static ReturnStatement Return(IExpression? Expr) => new(Expr);
    public static BreakStatement Break() => new();
    public static ContinueStatement Continue() => new();
    public static VariableOrValueStatement VarDeclaration(VariableDeclaration variable) => new(variable);
    public static IfStatement If(IExpression expr, CompoundStatement trueBody, CompoundStatement falseBody) => new(expr, trueBody, falseBody, []);


}
