using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Test;

internal static class RegionFixture
{
    internal sealed record BodySpec(
        Label Label,
        ImmutableArray<IShaderValue> Parameters,
        ImmutableArray<Instruction<IShaderValue, IShaderValue>> Instructions,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> Terminator);

    internal static BodySpec Body(
        Label label,
        ImmutableArray<IShaderValue> parameters,
        IEnumerable<Instruction<IShaderValue, IShaderValue>> instructions,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        new(label, parameters, [.. instructions], terminator);

    internal static FunctionBody4 CreateFunctionBody(
        FunctionDeclaration declaration,
        RegionTree<Label, BodySpec> region)
    {
        var blocks = new Dictionary<Label, BodySpec>();
        region.Traverse((_, label, body) =>
        {
            blocks.Add(label, body);
            return false;
        });
        var graph = new ControlFlowGraph<BodySpec>(
            region.Label,
            blocks.ToDictionary(
                item => item.Key,
                item => new ControlFlowGraph<BodySpec>.NodeDefinition(
                    item.Value.Terminator.ToSuccessor(),
                    item.Value)));
        var postDominance = graph.ControlFlowAnalysis().PostDominatorTree;
        return new FunctionBody4(
            declaration,
            region.Select(
                static label => label,
                body => ShaderRegionBody.Create(
                    body.Label,
                    body.Parameters,
                    body.Instructions,
                    body.Terminator,
                    postDominance.ExitPostDominance(body.Label))));
    }
}
