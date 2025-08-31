using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.CLSL.Language.Analysis;

internal class LocalVariableAnalysis
{
}

public static class LocalVariableAnalysisExtensions
{
    public static IEnumerable<VariableDeclaration> GetLocalVariables(this FunctionBody4 body)
        => body.GetUsedValues().OfType<VariablePointerValue>().Select(v => v.Declaration);
}
