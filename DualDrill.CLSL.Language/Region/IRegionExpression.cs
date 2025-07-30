namespace DualDrill.CLSL.Language.Region;


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
