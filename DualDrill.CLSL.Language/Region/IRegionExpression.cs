namespace DualDrill.CLSL.Language.Region;


public abstract record class RegionExpression<TL, TB>(TL Label)
{
}

sealed record class LabelRegionExpression<TL, TB>(TL Label) : RegionExpression<TL, TB>(Label)
{
}

sealed record class DefinitionRegionExpression<TL, TB>(RegionDefinition<TL, TB> Region) : RegionExpression<TL, TB>(Region.Label)
{
}

public static class RegionExpression
{
    public static RegionExpression<TL, TB> Label<TL, TB>(TL label)
        => new LabelRegionExpression<TL, TB>(label);
    public static RegionExpression<TL, TB> Definition<TL, TB>(RegionDefinition<TL, TB> region)
        => new DefinitionRegionExpression<TL, TB>(region);

    public static RegionExpression<TL, TB> AsRegionExpression<TL, TP, TB>(this RegionDefinition<TL, TB> region)
        => Definition(region);
}

