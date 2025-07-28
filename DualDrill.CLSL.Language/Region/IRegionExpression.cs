using System.Reflection.Emit;

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



public sealed record class RegionDefinition<TL, TP, TB>(IRegionDefinition<TL, IReadOnlyList<TP>, ISeq<RegionDefinition<TL, TP, TB>, TB>> Definition)
{
    public override string ToString()
        => Definition.ToString() ?? $"^{Label}: ({Parameters})";

    public TL Label => Definition.Label;
    public IReadOnlyList<TP> Parameters => Definition.Parameters;
    public TB Body => Definition.Body.Last;

    public static RegionDefinition<TL, TP, TB> Block(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body)
        => new(new BlockRegionDefinition<TL, IReadOnlyList<TP>, ISeq<RegionDefinition<TL, TP, TB>, TB>>(label, parameters, Seq.Create(regions, body)));
    public static RegionDefinition<TL, TP, TB> Loop(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body)
        => new(new LoopRegionDefinition<TL, IReadOnlyList<TP>, ISeq<RegionDefinition<TL, TP, TB>, TB>>(label, parameters, Seq.Create(regions, body)));

    public RegionDefinition<TL, TP, TB> WithBindings(IEnumerable<RegionDefinition<TL, TP, TB>> regions)
        => new(Definition.Select(seq => Seq.Create([.. regions], seq.Last)));

}

public abstract record class RegionExpression<TL, TP, TB>(TL Label)
{
}

sealed record class LabelRegionExpression<TL, TP, TB>(TL Label) : RegionExpression<TL, TP, TB>(Label)
{
}

sealed record class DefinitionRegionExpression<TL, TP, TB>(RegionDefinition<TL, TP, TB> Region) : RegionExpression<TL, TP, TB>(Region.Label)
{
}

public static class Region
{

    public static RegionDefinition<TL, TP, TB> Block<TL, TP, TB>(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body) =>
      RegionDefinition<TL, TP, TB>.Block(label, regions, parameters, body);
    public static RegionDefinition<TL, TP, TB> Loop<TL, TP, TB>(TL label, ReadOnlySpan<RegionDefinition<TL, TP, TB>> regions, IReadOnlyList<TP> parameters, TB body) =>
      RegionDefinition<TL, TP, TB>.Loop(label, regions, parameters, body);

}
