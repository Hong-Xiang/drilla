﻿namespace DualDrill.CLSL.Language.Region;

sealed record class LoopRegionDefinition<TL, TB>(TL Label, TB Body, TL? Next, TL? BreakNext) : IRegionDefinition<TL, TB>
{
    public RegionKind Kind => RegionKind.Loop;
    public TR Evaluate<TR>(IRegionDefinitionSemantic<TL, TB, TR> semantic)
      => semantic.Loop(Label, Body, Next, BreakNext);

    public IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f)
        => new LoopRegionDefinition<TL, TR>(Label, f(Body), Next, BreakNext);

}
