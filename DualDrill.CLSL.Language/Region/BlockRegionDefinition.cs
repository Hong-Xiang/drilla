namespace DualDrill.CLSL.Language.Region;

internal sealed record class BlockRegionDefinition<TL, TB>(TL Label, TB Body, TL? Next) : IRegionDefinition<TL, TB>
{
    public RegionKind Kind => RegionKind.Block;

    public TR Evaluate<TR>(IRegionDefinitionSemantic<TL, TB, TR> semantic) => semantic.Block(Label, Body, Next);

    public IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f) =>
        new BlockRegionDefinition<TL, TR>(Label, f(Body), Next);
}