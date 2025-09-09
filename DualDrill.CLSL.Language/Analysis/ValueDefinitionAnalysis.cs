using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Analysis;

internal class ValueDefinitionAnalysis
    : IRegionTreeFoldSemantic<Label, ShaderRegionBody, IEnumerable<IShaderValue>, IEnumerable<IShaderValue>>
    , IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, IEnumerable<IShaderValue>>
{
    public IEnumerable<IShaderValue> Block(Label label, Func<IEnumerable<IShaderValue>> body, Label? next)
        => body();

    public IEnumerable<IShaderValue> Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
        => [result];

    public IEnumerable<IShaderValue> Dup(IShaderValue result, IShaderValue source)
        => [result];

    public IEnumerable<IShaderValue> Get(IShaderValue result, IShaderValue source)
        => [result];

    public IEnumerable<IShaderValue> Let(IShaderValue result, ShaderExpr expr)
        => [result];

    public IEnumerable<IShaderValue> Loop(Label label, Func<IEnumerable<IShaderValue>> body, Label? next, Label? breakNext)
        => body();

    public IEnumerable<IShaderValue> Mov(IShaderValue target, IShaderValue source)
        => [];

    public IEnumerable<IShaderValue> Nested(IEnumerable<IShaderValue> head, Func<IEnumerable<IShaderValue>> next)
        => [.. head, .. next()];

    public IEnumerable<IShaderValue> Nop()
        => [];

    public IEnumerable<IShaderValue> Pop(IShaderValue target)
        => [];

    public IEnumerable<IShaderValue> Set(IShaderValue target, IShaderValue source)
        => [];

    public IEnumerable<IShaderValue> SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
        => [];

    public IEnumerable<IShaderValue> Single(ShaderRegionBody value)
    {
        return [
            ..value.Parameters,
            ..value.Body.Elements.SelectMany(s => s.Evaluate(this))
        ];
    }
}

public static class ValueDefinitionAnalysisExtension
{
    public static IEnumerable<IShaderValue> GetValueDefinitions(this FunctionBody4 body)
        => ((IEnumerable<IShaderValue>)[
            ..body.Declaration.Parameters.Select(p => p.Value),
            ..body.Body.Fold(new ValueDefinitionAnalysis())
        ]).Distinct();
}
