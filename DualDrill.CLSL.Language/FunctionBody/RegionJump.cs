using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class RegionJump(Label Label, ImmutableArray<IShaderValue> Arguments)
{
}

public sealed record class RegionJump<TV>(Label Label, ImmutableArray<TV> Arguments)
{
}

