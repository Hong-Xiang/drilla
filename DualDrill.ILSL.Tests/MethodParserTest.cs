using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.ILSL.Frontend;

namespace DualDrill.ILSL.Tests;


public class MethodParserTest
{
    private IMethodParser Parser { get; } = new ILSpyFrontend(new() { HotReloadAssemblies = [] });

    [Fact]
    public void ParseBasicReturnStatementShouldSucceed()
    {
        var result = Parser.ParseMethodBody(static () =>
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
}
