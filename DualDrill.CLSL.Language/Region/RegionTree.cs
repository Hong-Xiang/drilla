using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Region;



public interface IRegionDefinitionFoldSemantic<TX, TL, TB, TS, T>
    : ISeqSemantic<TX, T, TB, Func<TX, TS>, TS>
    , IRegionDefinitionSemantic<TX, TL, Func<TX, TS>, T>
{
}

public sealed record class RegionTree<TL, TB>(IRegionDefinition<TL, Seq<RegionTree<TL, TB>, TB>> Definition)
{
    public TL Label => Definition.Label;
    public TB Body => Definition.Body.Last;
    public IEnumerable<RegionTree<TL, TB>> Bindings => Definition.Body.Elements;

    public static RegionTree<TL, TB> Block(TL label, ReadOnlySpan<RegionTree<TL, TB>> regions, TB body)
        => new(new BlockRegionDefinition<TL, Seq<RegionTree<TL, TB>, TB>>(label, Seq.Create(regions, body)));
    public static RegionTree<TL, TB> Loop(TL label, ReadOnlySpan<RegionTree<TL, TB>> regions, TB body)
        => new(new LoopRegionDefinition<TL, Seq<RegionTree<TL, TB>, TB>>(label, Seq.Create(regions, body)));

    public RegionTree<TL, TB> WithBindings(IEnumerable<RegionTree<TL, TB>> regions)
        => new(Definition.Select(seq => Seq.Create([.. regions], seq.Last)));

    public T Fold<TX, TS, T>(IRegionDefinitionFoldSemantic<TX, TL, TB, TS, T> semantic, TX context)
        => Definition.Evaluate(new FoldImplSemantic<TX, TS, T>(semantic), context);


    sealed class FoldImplSemantic<TX, TS, T>(IRegionDefinitionFoldSemantic<TX, TL, TB, TS, T> semantic)
        : IRegionDefinitionSemantic<TX, TL, Seq<RegionTree<TL, TB>, TB>, T>
        , ISeqSemantic<TX, RegionTree<TL, TB>, TB, Func<TX, TS>, TS>
    {
        public T Block(TX context, TL label, Seq<RegionTree<TL, TB>, TB> body)
            => semantic.Block(context, label, ctx => body.Fold(this, ctx));
        public T Loop(TX context, TL label, Seq<RegionTree<TL, TB>, TB> body)
            => semantic.Loop(context, label, ctx => body.Fold(this, ctx));

        public TS Nested(TX context, RegionTree<TL, TB> head, Func<TX, TS> next)
            => semantic.Nested(context, head.Definition.Evaluate(this, context), next);

        public TS Single(TX context, TB value)
           => semantic.Single(context, value);
    }


    sealed class MapFoldSemantic<TLR, TBR>(Func<TL, TLR> l, Func<TB, TBR> b)
        : IRegionDefinitionFoldSemantic<Unit, TL, TB, Seq<RegionTree<TLR, TBR>, TBR>, RegionTree<TLR, TBR>>
    {
        public RegionTree<TLR, TBR> Block(Unit context, TL label, Func<Unit, Seq<RegionTree<TLR, TBR>, TBR>> body)
        {
            var bd = body(default);
            return RegionTree<TLR, TBR>.Block(l(label), [.. bd.Elements], bd.Last);
        }
        public RegionTree<TLR, TBR> Loop(Unit context, TL label, Func<Unit, Seq<RegionTree<TLR, TBR>, TBR>> body)
        {
            var bd = body(default);
            return RegionTree<TLR, TBR>.Loop(l(label), [.. bd.Elements], bd.Last);
        }

        public Seq<RegionTree<TLR, TBR>, TBR> Nested(Unit context, RegionTree<TLR, TBR> head, Func<Unit, Seq<RegionTree<TLR, TBR>, TBR>> next)
            => Seq<RegionTree<TLR, TBR>, TBR>.Nested(head, next(default));

        public Seq<RegionTree<TLR, TBR>, TBR> Single(Unit context, TB value)
            => Seq<RegionTree<TLR, TBR>, TBR>.Single(b(value));
    }

    public RegionTree<TLR, TBR> Map<TLR, TBR>(Func<TL, TLR> l, Func<TB, TBR> b)
        => Fold(new MapFoldSemantic<TLR, TBR>(l, b), default);
}

public static class RegionTree
{
    public static RegionTree<TL, TB> Block<TL, TB>(TL label, ReadOnlySpan<RegionTree<TL, TB>> regions, TB body)
        => RegionTree<TL, TB>.Block(label, regions, body);
    public static RegionTree<TL, TB> Loop<TL, TB>(TL label, ReadOnlySpan<RegionTree<TL, TB>> regions, TB body)
        => RegionTree<TL, TB>.Loop(label, regions, body);

    public static RegionTree<Label, TB> Create<TB>(
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

        RegionTree<Label, TB> ToRegion(Label l)
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
    public static string Show<TE>(this RegionTree<Label, ITerminator<Label, TE>> region)
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

    sealed class SuccessorFormatSemantic(IReadOnlyDictionary<Label, int> labels)
        : ISuccessorSemantic<Unit, Label, string>
    {
        public string LabelName(Label l) => $"^{labels[l]}:{l.Name}";

        public string Conditional(Unit context, Label trueTarget, Label falseTarget)
            => $"br_if -> {LabelName(trueTarget)} {LabelName(falseTarget)}";

        public string Terminate(Unit context)
            => "return";

        public string Unconditional(Unit context, Label target)
            => $"br -> {LabelName(target)}";
    }

    public static string Show(this RegionTree<Label, ISuccessor> region)
    {
        var labels = region.Fold(new RegionDefinitionDefinedLabelsSemantic<ISuccessor>(), default).Distinct()
                           .Select((l, i) => (l, i))
                           .ToFrozenDictionary(x => x.l, x => x.i);

        var stepFormatter = new SuccessorFormatSemantic(labels);

        var sir = region.Map<string, IEnumerable<string>>(stepFormatter.LabelName, s => [s.Evaluate(stepFormatter, default)]);
        var lines = sir.Fold(new RegionDefinitionFormatter(), 0);
        return string.Join(Environment.NewLine, lines);
    }
}
