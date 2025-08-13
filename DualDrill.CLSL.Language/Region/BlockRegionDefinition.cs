namespace DualDrill.CLSL.Language.Region;

sealed record class BlockRegionDefinition<TL, TB>(TL Label, TB Body) : IRegionDefinition<TL, TB>
{
    public RegionKind Kind => RegionKind.Block;

    public TR Evaluate<TX, TR>(IRegionDefinitionSemantic<TX, TL, TB, TR> semantic)
      => semantic.Block(Label, Body);

    public IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f)
        => new BlockRegionDefinition<TL, TR>(Label, f(Body));

}
