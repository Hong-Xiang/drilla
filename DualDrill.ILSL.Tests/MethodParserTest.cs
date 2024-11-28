using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.ILSL.Frontend;
using System.Numerics;

namespace DualDrill.ILSL.Tests;


public class MethodParserTest
{
    private IMethodParser Parser { get; } = new ILSpyMethodParser(new() { HotReloadAssemblies = [] });

    [Fact]
    public void ParseBasicReturnStatementShouldSucceed()
    {
        var result = Parser.ParseMethodBody(MethodParseContext.Empty, static () =>
        {
            return 42;
        });

        Assert.Single(result.Statements);
        var s = result.Statements[0];
        Assert.True(s is ReturnStatement
        {
            Expr: LiteralValueExpression
            {
                Literal: IntLiteral
                {
                    Value: 42
                },
                Type: IntType { BitWidth: N32 }
            }
        }, "Parse result should be correct return statement");
    }

    [Fact]
    public void ParseBasicArgument()
    {
        var result = Parser.ParseMethodBody(MethodParseContext.Empty, static (int a) =>
        {
            return a + 1;
        });

        Assert.Single(result.Statements);
        var s = result.Statements[0];
        Assert.True(s is ReturnStatement
        {
            Expr: BinaryArithmeticExpression
            {
                L: VariableIdentifierExpression
                {
                    Variable:
                    {
                        Name: "a",
                        Type: IntType { BitWidth: N32 }
                    }
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
            }
        }, "Parse result should be correct return statement");
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
