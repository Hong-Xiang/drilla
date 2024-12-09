using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.ILSL.Frontend;
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
        parser.Context.GetMethodContext(m);
        var body = methodParser.ParseMethodBody(parser.Context.GetMethodContext(m), m);
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
            Literal: IntLiteral { Value: 42 }
        });
    }

    [Fact]
    public void ParseBasicArgument()
    {
        var expr = ParseExpressionBodyMethod(MethodHelper.GetMethod(static (int a) => a + 1));
        Assert.True(expr is BinaryArithmeticExpression
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
        var expr = ParseExpressionBodyMethod(MethodHelper.GetMethod(() => new Vector4(0.5f, 0.0f, 1.0f, 1.0f)));
        Assert.True(expr is FunctionCallExpression
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
            Arguments: [LiteralValueExpression { Literal: IntLiteral { Value: 1 } },
                        LiteralValueExpression { Literal: IntLiteral { Value: 2 } }]
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
    }

    [Fact]
    public void Vec4DotTest()
    {
        var stmt = ParseStatementsMethod(MethodHelper.GetMethod(static (Vector4 a, Vector4 b) =>
        {
            return Vector4.Dot(a, b);
        }));
        Assert.True(stmt[0] is SimpleAssignmentStatement
        {
            R: FunctionCallExpression
            {
                Callee: { Name: "dot" },
                Arguments:
            [
                VariableIdentifierExpression
            {
                Variable: ParameterDeclaration
                {
                    Type: IVecType
                    {
                        Size: N4,
                        ElementType: FloatType
                    },
                    Name: "a"
                }
            },
                VariableIdentifierExpression
            {
                Variable: ParameterDeclaration
                {
                    Type: IVecType
                    {
                        Size: N4,
                        ElementType: FloatType
                    },
                    Name: "b"
                }
            },
            ]
            }
        });
        Assert.True(stmt[1] is ReturnStatement
        {
            Expr:
            {
                Type: FloatType
            }
        });
    }
}
