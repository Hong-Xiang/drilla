using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.ILSL.Frontend;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Tests;


public class ParseBodyTest
{
    private IMethodParser Parser { get; } = new ILSpyMethodParser(new() { HotReloadAssemblies = [] });

    IExpression? ParseExpressionBodyMethod(MethodBase m)
    {
        using var methodParser = new ILSpyMethodParser(new());
        var parser = new CLSLParser(methodParser);
        parser.Context.GetMethodContext(m);
        var body = methodParser.ParseMethodBody(parser.Context.GetMethodContext(m), m);
        Assert.Single(body.Statements);
        Assert.IsType<ReturnStatement>(body.Statements[0]);
        return ((ReturnStatement)body.Statements[0]).Expr;
    }


    IExpression ParseExpressionBodyMethod<T>(Func<T> f)
    {
        var result = ParseExpressionBodyMethod(f.Method);
        Assert.NotNull(result);
        return result;
    }

    IExpression ParseExpressionBodyMethod<TA, TB>(Func<TA, TB> f)
    {
        var result = ParseExpressionBodyMethod(f.Method);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public void ParseLiteralExpressionBodyTest()
    {
        var expr = ParseExpressionBodyMethod(static () => 42);
        Assert.Equal(ShaderType.I32, expr.Type);
        Assert.IsType<LiteralValueExpression>(expr);
        Assert.True(expr is LiteralValueExpression
        {
            Literal: IntLiteral { Value: 42 }
        });
    }

    [Fact]
    public void ParseBasicArgument()
    {
        var context = ParserContext.Create();
        var result = ParseExpressionBodyMethod(static (int a) => a + 1);

        Assert.True(result is BinaryArithmeticExpression
        {
            L: VariableIdentifierExpression
            {
                Variable: ParameterDeclaration
                {
                    Name: "a"
                },
            },
            R: LiteralValueExpression
            {
                Literal: IntLiteral
                {
                    Value: 1
                },
                Type: IntType { BitWidth: N32 }
            },
            Op: BinaryArithmeticOp.Addition,
            Type: IntType { BitWidth: N32 }
        });
    }


    [Fact]
    public void SimpleVec4ConstructorTest()
    {
        using var parser = new ILSpyMethodParser(new ILSpyOption { HotReloadAssemblies = [] });
        var parsed = parser.ParseMethodBody(MethodParseContext.Empty, static () =>
        {
            return new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
        });
        Assert.Single(parsed.Statements);
        Assert.True(parsed.Statements[0] is ReturnStatement
        {
            Expr: FunctionCallExpression
            {
                Arguments: { Length: 4 },
                Callee:
                {
                    Name: "vec4", Return:
                    {
                        Type: VecType
                        {
                            ElementType: FloatType { BitWidth: N32 },
                            Size: N4
                        }
                    }
                }
            }
        }, "Parse result should be correct return statement");
    }

    [Fact]
    public void BasicMethodInvocationParseTest()
    {
        static int Add(int a, int b) => a + b;
        var method = ((Func<int, int, int>)Add).Method;
        var decl = new FunctionDeclaration(method.Name, [], new FunctionReturn(ShaderType.I32, []), []);
        var env = MethodParseContext.Empty with
        {
            Methods = new Dictionary<MethodBase, FunctionDeclaration>()
            {
                [method] = decl
            }.ToImmutableDictionary()
        };
        using var parser = new ILSpyMethodParser(new ILSpyOption());
        var parsed = parser.ParseMethodBody(env, static () => Add(1, 2));
        Assert.Single(parsed.Statements);
        Assert.True(parsed.Statements[0] is ReturnStatement
        {
            Expr: FunctionCallExpression
            {
                Callee: var callee,
                Arguments: { Length: 2 }
            }
        } && callee.Equals(decl), "Parse result should be correct return statement");
    }


    [Fact]
    public void Vec4DotTest()
    {
        using var parser = new ILSpyMethodParser(new ILSpyOption { HotReloadAssemblies = [] });
        var result = parser.ParseMethodBody(MethodParseContext.Empty, static () =>
        {
            var v1 = new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
            var v2 = new Vector4(0.5f, 0.0f, 1.0f, 1.0f);
            return Vector4.Dot(v1, v2);
        });
        //var vec4CreateExpr = new FunctionCallExpression(
        //                IVecType<R4, FloatType>.Constructors[4],
        //                [
        //                    new LiteralValueExpression(new FloatLiteral(0.5f)),
        //                    new LiteralValueExpression(new FloatLiteral(0.0f)),
        //                    new LiteralValueExpression(new FloatLiteral(1.0f)),
        //                    new LiteralValueExpression(new FloatLiteral(1.0f)),
        //                ]);
        //var v1 = new VariableDeclaration(DeclarationScope.Function,
        //                                 "v1",
        //                                 new VecType<R4, FloatType>(),
        //                                 []);
        //var v2 = new VariableDeclaration(DeclarationScope.Function,
        //                                 "v2",
        //                                 new VecType<R4, FloatType>(),
        //                                 []);
        //AssertFunctionParsedCorrectly(new FloatType(),
        //    [
        //        new VariableOrValueStatement( v1 ),
        //        new VariableOrValueStatement( v2 ),
        //        new ReturnStatement(
        //            new FunctionCallExpression(
        //                IVecType<R4, FloatType>.Dot,
        //                [
        //                    new VariableIdentifierExpression(v1),
        //                    new VariableIdentifierExpression(v2),
        //                ])
        //        )
        //    ], parsed);
    }

}
