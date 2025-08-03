using DualDrill.CLSL.Language.Literal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

using static DualDrill.CLSL.Language.Expression.SyntaxFactory<DualDrill.CLSL.Language.Symbol.Label>;

namespace DualDrill.CLSL.Test;

public class ExpressionTreeTests(ITestOutputHelper Output)
{
    [Fact]
    public void SimpleArithExprDumpTest()
    {
        var e0 = LiteralExpr(Literal.Create(42));
        Output.WriteLine(e0.ToString());
    }

}
