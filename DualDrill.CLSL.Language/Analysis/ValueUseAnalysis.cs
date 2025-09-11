using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Analysis;

internal class ValueUseAnalysis
    : IRegionTreeFoldSemantic<Label, ShaderRegionBody, IEnumerable<IShaderValue>, IEnumerable<IShaderValue>>
    , IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, IEnumerable<IShaderValue>>
    , IExpressionTreeFoldSemantic<IShaderValue, IEnumerable<IShaderValue>>
    , ITerminatorSemantic<RegionJump, IShaderValue, IEnumerable<IShaderValue>>
{
    public IEnumerable<IShaderValue> AddressOfChain(IAccessChainOperation operation, IEnumerable<IShaderValue> e)
        => e;

    public IEnumerable<IShaderValue> AddressOfIndex(IAccessChainOperation operation, IEnumerable<IShaderValue> e, IEnumerable<IShaderValue> index)
        => [.. e, .. index];

    public IEnumerable<IShaderValue> Block(Label label, Func<IEnumerable<IShaderValue>> body, Label? next)
        => body();

    public IEnumerable<IShaderValue> Br(RegionJump target)
        => [..target.Arguments];

    public IEnumerable<IShaderValue> BrIf(IShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
        => [condition, ..trueTarget.Arguments, ..falseTarget.Arguments];

    public IEnumerable<IShaderValue> Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
        => arguments;

    public IEnumerable<IShaderValue> Dup(IShaderValue result, IShaderValue source)
        => [source];

    public IEnumerable<IShaderValue> Get(IShaderValue result, IShaderValue source)
        => [source];

    public IEnumerable<IShaderValue> Let(IShaderValue result, ShaderExpr expr)
        => [.. expr.Fold(this)];

    public IEnumerable<IShaderValue> Literal<TLiteral>(TLiteral literal) where TLiteral : ILiteral
        => [];

    public IEnumerable<IShaderValue> Loop(Label label, Func<IEnumerable<IShaderValue>> body, Label? next, Label? breakNext)
        => body();

    public IEnumerable<IShaderValue> Mov(IShaderValue target, IShaderValue source)
        => [source];

    public IEnumerable<IShaderValue> Nested(IEnumerable<IShaderValue> head, Func<IEnumerable<IShaderValue>> next)
        => [.. head, .. next()];

    public IEnumerable<IShaderValue> Nop()
        => [];

    public IEnumerable<IShaderValue> Operation1(IUnaryExpressionOperation operation, IEnumerable<IShaderValue> e)
        => e;

    public IEnumerable<IShaderValue> Operation2(IBinaryExpressionOperation operation, IEnumerable<IShaderValue> l, IEnumerable<IShaderValue> r)
        => [.. l, .. r];

    public IEnumerable<IShaderValue> Pop(IShaderValue target)
        => [target];

    public IEnumerable<IShaderValue> ReturnExpr(IShaderValue expr)
        => [expr];

    public IEnumerable<IShaderValue> ReturnVoid()
        => [];

    public IEnumerable<IShaderValue> Set(IShaderValue target, IShaderValue source)
        => [target, source];

    public IEnumerable<IShaderValue> SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
        => [target, value];

    public IEnumerable<IShaderValue> Single(ShaderRegionBody value)
    {
        return [
            ..value.Body.Elements.SelectMany(s => s.Operands),
            ..value.Body.Last.Evaluate(this)
        ];
    }

    public IEnumerable<IShaderValue> Value(IShaderValue value)
        => [value];

    public IEnumerable<IShaderValue> VectorCompositeConstruction(VectorCompositeConstructionOperation operation, IEnumerable<IEnumerable<IShaderValue>> arguments)
        => arguments.SelectMany(a => a);
}

public static class ValueUseAnalysisExtension
{
    public static IEnumerable<IShaderValue> GetUsedValues(this FunctionBody4 body)
        => ((IEnumerable<IShaderValue>)[
            ..body.Body.Fold(new ValueUseAnalysis())
        ]).Distinct();
}
