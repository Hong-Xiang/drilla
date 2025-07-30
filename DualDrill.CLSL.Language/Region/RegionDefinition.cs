using DualDrill.CLSL.Language.ControlFlowGraph;
using System.Collections.Immutable;
using System.Reactive;

namespace DualDrill.CLSL.Language.Region;

public interface IRegionDefinitionSemantic<in TL, in TP, in TB, out TO>
{
    TO Block(TL label, TP parameters, TB body);
    TO Loop(TL label, TP parameters, TB body);
}


public interface IRegionDefinition<out TL, out TP, out TB>
{
    TL Label { get; }
    TP Parameters { get; }
    TB Body { get; }
    TR Evaluate<TR>(IRegionDefinitionSemantic<TL, TP, TB, TR> semantic);
    IRegionDefinition<TL, TP, TR> Select<TR>(Func<TB, TR> f);
}


sealed record class BlockRegionDefinition<TL, TP, TB>(TL Label, TP Parameters, TB Body) : IRegionDefinition<TL, TP, TB>
{
    public override string ToString()
        => $"block ^{Label}: ({Parameters}){Environment.NewLine}\t{Body}{Environment.NewLine}";

    public TR Evaluate<TR>(IRegionDefinitionSemantic<TL, TP, TB, TR> semantic)
      => semantic.Block(Label, Parameters, Body);

    public IRegionDefinition<TL, TP, TR> Select<TR>(Func<TB, TR> f)
        => new BlockRegionDefinition<TL, TP, TR>(Label, Parameters, f(Body));
}

sealed record class LoopRegionDefinition<TL, TP, TB>(TL Label, TP Parameters, TB Body) : IRegionDefinition<TL, TP, TB>
{
    public override string ToString()
        => $"loop ^{Label}: ({Parameters}){Environment.NewLine}\t{Body}{Environment.NewLine}";

    public TR Evaluate<TR>(IRegionDefinitionSemantic<TL, TP, TB, TR> semantic)
      => semantic.Loop(Label, Parameters, Body);

    public IRegionDefinition<TL, TP, TR> Select<TR>(Func<TB, TR> f)
        => new LoopRegionDefinition<TL, TP, TR>(Label, Parameters, f(Body));
}


interface RegionDefinitionFoldSemantic<TL, TP, TB, T>
    : ISeqSemantic<T, TB, Unit, T, T>
    , IRegionDefinitionSemantic<TL, TP, TB, T>
{
}

public sealed record class RegionDefinition<TL, TP, TB>(IRegionDefinition<TL, IReadOnlyList<TP>, Seq<RegionDefinition<TL, TP, TB>, TB>> Definition)
{
    public override string ToString()
        => Definition.ToString() ?? $"^{Label}: ({Parameters})";

    public TL Label => Definition.Label;
    public IReadOnlyList<TP> Parameters => Definition.Parameters;
    public TB Body => Definition.Body.Last;
    public IEnumerable<RegionDefinition<TL, TP, TB>> Bindings => Definition.Body.Elements;

    public static RegionDefinition<TL, TP, TB> Block(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body)
        => new(new BlockRegionDefinition<TL, IReadOnlyList<TP>, Seq<RegionDefinition<TL, TP, TB>, TB>>(label, parameters, Seq.Create(regions, body)));
    public static RegionDefinition<TL, TP, TB> Loop(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body)
        => new(new LoopRegionDefinition<TL, IReadOnlyList<TP>, Seq<RegionDefinition<TL, TP, TB>, TB>>(label, parameters, Seq.Create(regions, body)));

    public RegionDefinition<TL, TP, TB> WithBindings(IEnumerable<RegionDefinition<TL, TP, TB>> regions)
        => new(Definition.Select(seq => Seq.Create([.. regions], seq.Last)));

}

public static class RegionDefinition
{
    public static RegionDefinition<TL, TP, TB> Block<TL, TP, TB>(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body)
        => RegionDefinition<TL, TP, TB>.Block(label, regions, parameters, body);
    public static RegionDefinition<TL, TP, TB> Loop<TL, TP, TB>(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body)
        => RegionDefinition<TL, TP, TB>.Loop(label, regions, parameters, body);

    public static RegionDefinition<ControlFlow.Label, TP, TB> Create<TP, TB>(
        ControlFlow.Label entry,
        Func<TB, ControlFlow.ISuccessor> successor,
        params IEnumerable<(ControlFlow.Label Label, IReadOnlyList<TP> Parameters, TB Body)> regions
    )
    {
        var regions_ = regions.ToDictionary(x => x.Label, x => x);
        // TODO: argument validation :
        // labels are unique in regions.
        // all successor target labels are defined in region labels.
        var cfg = ControlFlow.ControlFlowGraph.Create(entry, regions_.ToDictionary(x => x.Key, x => successor(x.Value.Body)));
        var cfr = cfg.ControlFlowAnalysis();
        var dt = cfr.DominatorTree;

        RegionDefinition<ControlFlow.Label, TP, TB> ToRegion(ControlFlow.Label l)
        {
            var children = dt.GetChildren(l);
            var childrenExpressions = children.Reverse().Select(ToRegion).ToImmutableArray();
            return cfr.IsLoop(l)
                ? Loop(l, [.. childrenExpressions], [.. regions_[l].Parameters], regions_[l].Body)
                : Block(l, [.. childrenExpressions], [.. regions_[l].Parameters], regions_[l].Body);
        }
        return ToRegion(cfg.EntryLabel);

    }
}
