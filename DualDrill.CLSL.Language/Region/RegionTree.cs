using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Region;



public interface IRegionTreeFoldSemantic<TL, TB, TS, T>
    : ISeqSemantic<T, TB, Func<TS>, TS>
    , IRegionDefinitionSemantic<TL, Func<TS>, T>
{
}

public interface IRegionTreeFoldLazySemantic<TL, TB, TS, T>
    : ISeqSemantic<Func<T>, TB, Func<TS>, TS>
    , IRegionDefinitionSemantic<TL, Func<TS>, T>
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

    public T Fold<TS, T>(IRegionTreeFoldSemantic<TL, TB, TS, T> semantic)
        => Definition.Evaluate(new FoldImplSemantic<TS, T>(semantic));
    public T Fold<TS, T>(IRegionTreeFoldLazySemantic<TL, TB, TS, T> semantic)
        => Definition.Evaluate(new FoldLazyImplSemantic<TS, T>(semantic))();


    public bool Traverse(Func<TL, TB, bool> f)
    {
        var r = f(Definition.Label, Definition.Body.Last);
        if (!r)
        {
            foreach (var b in Definition.Body.Elements.Reverse())
            {
                if (b.Traverse(f))
                {
                    return true;
                }
            }
        }
        return r;
    }

    sealed class FoldLazyImplSemantic<TS, T>(IRegionTreeFoldLazySemantic<TL, TB, TS, T> semantic)
        : IRegionDefinitionSemantic<TL, Seq<RegionTree<TL, TB>, TB>, Func<T>>
        , ISeqSemantic<RegionTree<TL, TB>, TB, Func<TS>, Func<TS>>
    {
        public Func<T> Block(TL label, Seq<RegionTree<TL, TB>, TB> body)
            => () => semantic.Block(label, body.Fold(this));
        public Func<T> Loop(TL label, Seq<RegionTree<TL, TB>, TB> body)
            => () => semantic.Loop(label, body.Fold(this));

        public Func<TS> Nested(RegionTree<TL, TB> head, Func<TS> next)
            => () => semantic.Nested(head.Definition.Evaluate(this), next);

        public Func<TS> Single(TB value)
           => () => semantic.Single(value);
    }




    sealed class FoldImplSemantic<TS, T>(IRegionTreeFoldSemantic<TL, TB, TS, T> semantic)
        : IRegionDefinitionSemantic<TL, Seq<RegionTree<TL, TB>, TB>, T>
        , ISeqSemantic<RegionTree<TL, TB>, TB, Func<TS>, TS>
    {
        public T Block(TL label, Seq<RegionTree<TL, TB>, TB> body)
            => semantic.Block(label, () => body.FoldLazy(this));
        public T Loop(TL label, Seq<RegionTree<TL, TB>, TB> body)
            => semantic.Loop(label, () => body.FoldLazy(this));

        public TS Nested(RegionTree<TL, TB> head, Func<TS> next)
            => semantic.Nested(head.Definition.Evaluate(this), next);

        public TS Single(TB value)
           => semantic.Single(value);
    }


    sealed class MapFoldSemantic<TLR, TBR>(Func<TL, TLR> l, Func<TB, TBR> b)
        : IRegionTreeFoldSemantic<TL, TB, Seq<RegionTree<TLR, TBR>, TBR>, RegionTree<TLR, TBR>>
    {
        public RegionTree<TLR, TBR> Block(TL label, Func<Seq<RegionTree<TLR, TBR>, TBR>> body)
        {
            var bd = body();
            return RegionTree<TLR, TBR>.Block(l(label), [.. bd.Elements], bd.Last);
        }
        public RegionTree<TLR, TBR> Loop(TL label, Func<Seq<RegionTree<TLR, TBR>, TBR>> body)
        {
            var bd = body();
            return RegionTree<TLR, TBR>.Loop(l(label), [.. bd.Elements], bd.Last);
        }

        public Seq<RegionTree<TLR, TBR>, TBR> Nested(RegionTree<TLR, TBR> head, Func<Seq<RegionTree<TLR, TBR>, TBR>> next)
            => Seq<RegionTree<TLR, TBR>, TBR>.Nested(head, next());

        public Seq<RegionTree<TLR, TBR>, TBR> Single(TB value)
            => Seq<RegionTree<TLR, TBR>, TBR>.Single(b(value));
    }

    public RegionTree<TLR, TBR> Map<TLR, TBR>(Func<TL, TLR> l, Func<TB, TBR> b)
        => Fold(new MapFoldSemantic<TLR, TBR>(l, b));
}

public static class RegionTree
{
    public static RegionTree<TL, TB> Block<TL, TB>(TL label, ReadOnlySpan<RegionTree<TL, TB>> regions, TB body)
        => RegionTree<TL, TB>.Block(label, regions, body);
    public static RegionTree<TL, TB> Loop<TL, TB>(TL label, ReadOnlySpan<RegionTree<TL, TB>> regions, TB body)
        => RegionTree<TL, TB>.Loop(label, regions, body);

    public static void Traverse(this RegionTree<Label, ShaderRegionBody> region, Action<RegionTree<Label, ShaderRegionBody>> f)
    {
        f(region);
        foreach (var child in region.Bindings)
        {
            child.Traverse(f);
        }
    }

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
sealed class RegionDefinitionFormatter(int indent)
   : IRegionTreeFoldSemantic<string, IEnumerable<string>, IEnumerable<string>, IEnumerable<string>>
{
    string IndentStr => new string('\t', Indent);

    private int Indent = indent;

    public IEnumerable<string> Block(string label, Func<IEnumerable<string>> body)
    {
        Indent++;
        var b = body();
        Indent--;
        return [IndentStr + $"block {label}", .. b];
    }

    public IEnumerable<string> Loop(string label, Func<IEnumerable<string>> body)
    {
        Indent++;
        var b = body();
        Indent--;
        return [IndentStr + $"loop {label}", .. body()];
    }

    public IEnumerable<string> Nested(IEnumerable<string> head, Func<IEnumerable<string>> next)
        => [.. head, .. next()];

    public IEnumerable<string> Single(IEnumerable<string> value)
        => value.Select(v => IndentStr + v);
}

sealed class RegionDefinitionDefinedLabelsSemantic<TB>
    : IRegionTreeFoldSemantic<Label, TB, IEnumerable<Label>, IEnumerable<Label>>
{
    public IEnumerable<Label> Block(Label label, Func<IEnumerable<Label>> body)
        => [label, .. body()];
    public IEnumerable<Label> Loop(Label label, Func<IEnumerable<Label>> body)
        => [label, .. body()];

    public IEnumerable<Label> Nested(IEnumerable<Label> head, Func<IEnumerable<Label>> next)
        => [.. head, .. next()];

    public IEnumerable<Label> Single(TB value)
        => [];
}

public static class RegionDefinitionExtension

{
    public static string Show<TE>(this RegionTree<Label, ITerminator<Label, TE>> region)
    {
        var labels = region.Fold(new RegionDefinitionDefinedLabelsSemantic<ITerminator<Label, TE>>()).Distinct()
                           .Select((l, i) => (l, i))
                           .ToFrozenDictionary(x => x.l, x => x.i);

        var labelToName = LabelMap.Create(l => $"^{l.Name}:{labels[l]}");

        var sir = region.Map<string, IEnumerable<string>>(
            labelToName.MapLabel, s =>
            [s.Map(labelToName).ToString() ?? string.Empty]);
        var lines = sir.Fold(new RegionDefinitionFormatter(0));
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
        var labels = region.Fold(new RegionDefinitionDefinedLabelsSemantic<ISuccessor>()).Distinct()
                           .Select((l, i) => (l, i))
                           .ToFrozenDictionary(x => x.l, x => x.i);

        var stepFormatter = new SuccessorFormatSemantic(labels);

        var sir = region.Map<string, IEnumerable<string>>(stepFormatter.LabelName, s => [s.Evaluate(stepFormatter, default)]);
        var lines = sir.Fold(new RegionDefinitionFormatter(0));
        return string.Join(Environment.NewLine, lines);
    }
}
