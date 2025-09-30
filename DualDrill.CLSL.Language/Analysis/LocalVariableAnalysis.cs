using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Language.Analysis;

internal class LocalVariableAnalysis
{
}

public static class LocalVariableAnalysisExtensions
{
    public static IEnumerable<VariableDeclaration> GetLocalVariables(this FunctionBody4 body)
    {
        return body.GetUsedValues().OfType<VariablePointerValue>().Select(v => v.Declaration);
    }
}