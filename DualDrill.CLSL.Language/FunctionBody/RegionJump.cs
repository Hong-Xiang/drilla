using System.Collections.Immutable;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class RegionJump<TValue>(Label Label, ImmutableArray<TValue> Arguments)
{
    public RegionJump<TNext> Select<TNext>(Func<TValue, TNext> map) =>
        new(Label, [.. Arguments.Select(map)]);
}