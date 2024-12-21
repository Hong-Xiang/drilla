using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.ILSL.Frontend;
using DualDrill.Mathematics;
using System.Numerics;
using System.Reflection;

namespace DualDrill.ILSL.Tests;


public class ParseBodyTest
{
    IExpression? ParseExpressionBodyMethod(MethodBase m)
    {
        var stmts = ParseStatementsMethod(m);
        Assert.Single(stmts);
        Assert.IsType<ReturnStatement>(stmts[0]);
        return ((ReturnStatement)stmts[0]).Expr;
    }

    IReadOnlyList<IStatement> ParseStatementsMethod(MethodBase m)
    {
        var methodParser = new RelooperMethodParser();
        var parser = new CLSLParser(methodParser);
        parser.ParseMethodMetadata(m);
        var methodContext = parser.Context.GetMethodContext(m);
        var body = methodParser.ParseMethodBody(methodContext, m);
        return body.Statements;
    }

    [Fact]
    public void ParseLiteralExpressionBodyTest()
    {
        var expr = ParseExpressionBodyMethod(MethodHelper.GetMethod(static () => 42));
        Assert.Equal(ShaderType.I32, expr.Type);
        Assert.IsType<LiteralValueExpression>(expr);
        Assert.True(expr is LiteralValueExpression
        {
            Literal: I32Literal { Value: 42 }
        });
    }

    [Fact]
    public void ParseBasicArgument()
    {
        var expr = ParseExpressionBodyMethod(MethodHelper.GetMethod(static (int a) => a + 1));
        Assert.True(expr is BinaryArithmeticExpression
        {
            L: FormalParameterExpression
            {
                Parameter:
                {
                    Name: "a",
                    Type: IntType<N32>
                }
            },
            R: LiteralValueExpression
            {
                Literal: I32Literal
                {
                    Value: 1
                },
                Type: IIntType { BitWidth: N32 }
            },
            Op: BinaryArithmetic.Op.Addition,
            Type: IIntType { BitWidth: N32 }
        });
    }


    [Fact]
    public void SimpleVec4ConstructorTest()
    {
        var expr = ParseExpressionBodyMethod(MethodHelper.GetMethod(() => new Vector4(0.5f, 0.0f, 1.0f, 1.0f)));
        Assert.True(expr is FunctionCallExpression
        {
            Arguments.Length: 4,
            Callee:
            {
                Name: "vec4", Return.Type: VecType<N4, FloatType<N32>>
            }
        }, "Parse result should be correct return statement");
    }


    [Fact]
    public void BasicMethodInvocationParseTest()
    {
        static int Add(int a, int b) => a + b;
        var method = ((Func<int, int, int>)Add).Method;
        var decl = new FunctionDeclaration(method.Name, [], new FunctionReturn(ShaderType.I32, []), []);
        var parsed = ParseExpressionBodyMethod(MethodHelper.GetMethod(static () => Add(1, 2)));
        Assert.True(parsed is FunctionCallExpression
        {
            Callee: var callee,
            Arguments: [LiteralValueExpression { Literal: I32Literal { Value: 1 } },
                        LiteralValueExpression { Literal: I32Literal { Value: 2 } }]
        } && callee.Name.Equals(method.Name), "Parse result should be correct return statement");
    }

    [Fact]
    public void BasicConditionTest()
    {
        var stmts = ParseStatementsMethod(MethodHelper.GetMethod(static (int a, int b) =>
        {
            if (a >= b)
            {
                return a;
            }
            else
            {
                return b;
            }
        }));
        Assert.Equal(4, stmts.Count);
        Assert.IsType<LoopStatement>(stmts[3]);
    }

    [Fact]
    public void VectorSwizzleGetterTest()
    {
        var expr = ParseExpressionBodyMethod(MethodHelper.GetMethod(static (vec4f32 v) => v.xyx));
        Assert.True(expr is VectorSwizzleAccessExpression
        {
            Base: FormalParameterExpression
            {
                Parameter.Name: "v",
                Type: VecType<N4, FloatType<N32>>
            },
            Components: [SwizzleComponent.x, SwizzleComponent.y, SwizzleComponent.x]
        });
    }

    [Fact]
    public void VectorSwizzleSetterTest()
    {
        var stmt = ParseStatementsMethod(MethodHelper.GetMethod(static (vec4f32 v4, vec2f32 v2) =>
        {
            v4.xy = v2;
            return v4;
        }));
    }

    [Fact]
    public void Vec4DotTest()
    {
        var stmt = ParseStatementsMethod(MethodHelper.GetMethod(static (Vector4 a, Vector4 b) =>
        {
            return Vector4.Dot(a, b);
        }));
        var value = Assert.IsType<SimpleAssignmentStatement>(stmt[1]);
        var fc = Assert.IsType<FunctionCallExpression>(value.R);
        Assert.Equal("dot", fc.Callee.Name);
        Assert.Equal(2, fc.Arguments.Length);
        Assert.True(fc.Arguments[0] is FormalParameterExpression
        {
            Type: VecType<N4, FloatType<N32>>,
            Parameter.Name: "a"
        });
        Assert.True(fc.Arguments[1] is FormalParameterExpression
        {
            Type: VecType<N4, FloatType<N32>>,
            Parameter.Name: "b"
        });
        Assert.True(stmt[2] is ReturnStatement
        {
            Expr.Type: FloatType<N32>
        });
    }

    [Fact]
    public void CompileTaneryConditionalExpressionTest()
    {
        var stmt = ParseStatementsMethod(MethodHelper.GetMethod(static (float a, float b, float c) =>
        {
            return c <= 0 ? a : b;
        }));
        Assert.Equal(3, stmt.Count);
    }
}
