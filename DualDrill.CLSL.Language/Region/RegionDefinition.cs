using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Region;

public interface IRegionDefinitionSemantic<in TX, in TL, in TB, out TO>
{
    TO Block(TX context, TL label, TB body);
    TO Loop(TX context, TL label, TB body);
}

public interface IRegionDefinition<out TL, out TB>
{
    TL Label { get; }
    TB Body { get; }
    TR Evaluate<TX, TR>(IRegionDefinitionSemantic<TX, TL, TB, TR> semantic, TX context);
    IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f);
}


sealed record class BlockRegionDefinition<TL, TB>(TL Label, TB Body) : IRegionDefinition<TL, TB>
{
    public TR Evaluate<TX, TR>(IRegionDefinitionSemantic<TX, TL, TB, TR> semantic, TX context)
      => semantic.Block(context, Label, Body);

    public IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f)
        => new BlockRegionDefinition<TL, TR>(Label, f(Body));

}

sealed record class LoopRegionDefinition<TL, TB>(TL Label, TB Body) : IRegionDefinition<TL, TB>
{

    public TR Evaluate<TX, TR>(IRegionDefinitionSemantic<TX, TL, TB, TR> semantic, TX context)
      => semantic.Loop(context, Label, Body);

    public IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f)
        => new LoopRegionDefinition<TL, TR>(Label, f(Body));

}


public interface IRegionDefinitionFoldSemantic<TX, TL, TB, TS, T>
    : ISeqSemantic<TX, T, TB, Func<TX, TS>, TS>
    , IRegionDefinitionSemantic<TX, TL, Func<TX, TS>, T>
{
}

public sealed record class RegionDefinition<TL, TB>(IRegionDefinition<TL, Seq<RegionDefinition<TL, TB>, TB>> Definition)
{
    public TL Label => Definition.Label;
    public TB Body => Definition.Body.Last;
    public IEnumerable<RegionDefinition<TL, TB>> Bindings => Definition.Body.Elements;

    public static RegionDefinition<TL, TB> Block(TL label, ReadOnlySpan<RegionDefinition<TL, TB>> regions, TB body)
        => new(new BlockRegionDefinition<TL, Seq<RegionDefinition<TL, TB>, TB>>(label, Seq.Create(regions, body)));
    public static RegionDefinition<TL, TB> Loop(TL label, ReadOnlySpan<RegionDefinition<TL, TB>> regions, TB body)
        => new(new LoopRegionDefinition<TL, Seq<RegionDefinition<TL, TB>, TB>>(label, Seq.Create(regions, body)));

    public RegionDefinition<TL, TB> WithBindings(IEnumerable<RegionDefinition<TL, TB>> regions)
        => new(Definition.Select(seq => Seq.Create([.. regions], seq.Last)));

    public T Fold<TX, TS, T>(IRegionDefinitionFoldSemantic<TX, TL, TB, TS, T> semantic, TX context)
        => Definition.Evaluate(new FoldImplSemantic<TX, TS, T>(semantic), context);


    sealed class FoldImplSemantic<TX, TS, T>(IRegionDefinitionFoldSemantic<TX, TL, TB, TS, T> semantic)
        : IRegionDefinitionSemantic<TX, TL, Seq<RegionDefinition<TL, TB>, TB>, T>
        , ISeqSemantic<TX, RegionDefinition<TL, TB>, TB, Func<TX, TS>, TS>
    {
        public T Block(TX context, TL label, Seq<RegionDefinition<TL, TB>, TB> body)
            => semantic.Block(context, label, ctx => body.Fold(this, ctx));
        public T Loop(TX context, TL label, Seq<RegionDefinition<TL, TB>, TB> body)
            => semantic.Loop(context, label, ctx => body.Fold(this, ctx));

        public TS Nested(TX context, RegionDefinition<TL, TB> head, Func<TX, TS> next)
            => semantic.Nested(context, head.Definition.Evaluate(this, context), next);

        public TS Single(TX context, TB value)
           => semantic.Single(context, value);
    }


    sealed class MapFoldSemantic<TLR, TBR>(Func<TL, TLR> l, Func<TB, TBR> b)
        : IRegionDefinitionFoldSemantic<Unit, TL, TB, Seq<RegionDefinition<TLR, TBR>, TBR>, RegionDefinition<TLR, TBR>>
    {
        public RegionDefinition<TLR, TBR> Block(Unit context, TL label, Func<Unit, Seq<RegionDefinition<TLR, TBR>, TBR>> body)
        {
            var bd = body(default);
            return RegionDefinition<TLR, TBR>.Block(l(label), [.. bd.Elements], bd.Last);
        }
        public RegionDefinition<TLR, TBR> Loop(Unit context, TL label, Func<Unit, Seq<RegionDefinition<TLR, TBR>, TBR>> body)
        {
            var bd = body(default);
            return RegionDefinition<TLR, TBR>.Loop(l(label), [.. bd.Elements], bd.Last);
        }

        public Seq<RegionDefinition<TLR, TBR>, TBR> Nested(Unit context, RegionDefinition<TLR, TBR> head, Func<Unit, Seq<RegionDefinition<TLR, TBR>, TBR>> next)
            => Seq<RegionDefinition<TLR, TBR>, TBR>.Nested(head, next(default));

        public Seq<RegionDefinition<TLR, TBR>, TBR> Single(Unit context, TB value)
            => Seq<RegionDefinition<TLR, TBR>, TBR>.Single(b(value));
    }

    public RegionDefinition<TLR, TBR> Map<TLR, TBR>(Func<TL, TLR> l, Func<TB, TBR> b)
        => Fold(new MapFoldSemantic<TLR, TBR>(l, b), default);
}

public static class RegionDefinition
{
    public static RegionDefinition<TL, TB> Block<TL, TB>(TL label, ReadOnlySpan<RegionDefinition<TL, TB>> regions, TB body)
        => RegionDefinition<TL, TB>.Block(label, regions, body);
    public static RegionDefinition<TL, TB> Loop<TL, TB>(TL label, ReadOnlySpan<RegionDefinition<TL, TB>> regions, TB body)
        => RegionDefinition<TL, TB>.Loop(label, regions, body);

    public static RegionDefinition<Label, TB> Create<TB>(
        Label entry,
        Func<TB, ControlFlow.ISuccessor> successor,
        params IEnumerable<(Label Label, TB Body)> regions
    )
    {
        var regions_ = regions.ToDictionary(x => x.Label, x => x);
        // TODO: argument validation :
        // labels are unique in regions.
        // all successor target labels are defined in region labels.
        var cfg = ControlFlow.ControlFlowGraph.Create(entry, regions_.ToDictionary(x => x.Key, x => successor(x.Value.Body)));
        var cfr = cfg.ControlFlowAnalysis();
        var dt = cfr.DominatorTree;

        RegionDefinition<Label, TB> ToRegion(Label l)
        {
            var children = dt.GetChildren(l);
            var childrenExpressions = children.Reverse().Select(ToRegion).ToImmutableArray();
            return cfr.IsLoop(l)
                ? Loop(l, [.. childrenExpressions], regions_[l].Body)
                : Block(l, [.. childrenExpressions], regions_[l].Body);
        }
        return ToRegion(cfg.EntryLabel);

    }

}
sealed class RegionDefinitionFormatter
   : IRegionDefinitionFoldSemantic<int, string, IEnumerable<string>, IEnumerable<string>, IEnumerable<string>>
{
    string Indent(int count) => new string('\t', count);

    public IEnumerable<string> Block(int context, string label, Func<int, IEnumerable<string>> body)
    {
        return [Indent(context) + $"block {label}", .. body(context + 1)];
    }

    public IEnumerable<string> Loop(int context, string label, Func<int, IEnumerable<string>> body)
    {
        return [Indent(context) + $"loop {label}", .. body(context + 1)];
    }

    public IEnumerable<string> Nested(int context, IEnumerable<string> head, Func<int, IEnumerable<string>> next)
        => [.. head, .. next(context)];

    public IEnumerable<string> Single(int context, IEnumerable<string> value)
        => value.Select(v => Indent(context) + v);
}

sealed class RegionDefinitionDefinedLabelsSemantic<TB>
    : IRegionDefinitionFoldSemantic<Unit, Label, TB, IEnumerable<Label>, IEnumerable<Label>>
{
    public IEnumerable<Label> Block(Unit context, Label label, Func<Unit, IEnumerable<Label>> body)
        => [label, .. body(default)];
    public IEnumerable<Label> Loop(Unit context, Label label, Func<Unit, IEnumerable<Label>> body)
        => [label, .. body(default)];

    public IEnumerable<Label> Nested(Unit context, IEnumerable<Label> head, Func<Unit, IEnumerable<Label>> next)
        => [.. head, .. next(default)];

    public IEnumerable<Label> Single(Unit context, TB value)
        => [];
}

public static class RegionDefinitionExtension

{
    public static string Show<TE>(this RegionDefinition<Label, ITerminator<Label, TE>> region)
    {
        var labels = region.Fold(new RegionDefinitionDefinedLabelsSemantic<ITerminator<Label, TE>>(), default).Distinct()
                           .Select((l, i) => (l, i))
                           .ToFrozenDictionary(x => x.l, x => x.i);

        var labelToName = LabelMap.Create(l => $"^{l.Name}:{labels[l]}");

        var sir = region.Map<string, IEnumerable<string>>(
            labelToName.MapLabel, s =>
            [s.Map(labelToName).ToString() ?? string.Empty]);
        var lines = sir.Fold(new RegionDefinitionFormatter(), 0);
        return string.Join(Environment.NewLine, lines);
    }

    public static string Show(this RegionDefinition<Label, ISuccessor> region)
    {
        var labels = region.Fold(new RegionDefinitionDefinedLabelsSemantic<ISuccessor>(), default).Distinct()
                           .Select((l, i) => (l, i))
                           .ToFrozenDictionary(x => x.l, x => x.i);


        var sir = region.Map<string, IEnumerable<string>>(l => $"^{l.Name}:{labels[l]}", s => [s.ToString() ?? string.Empty]);
        var lines = sir.Fold(new RegionDefinitionFormatter(), 0);
        return string.Join(Environment.NewLine, lines);
    }
}
