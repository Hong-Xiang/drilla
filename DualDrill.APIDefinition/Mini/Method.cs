using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public sealed record class Method(
    string Name,
    ImmutableArray<Parameter>? Parameters,
    ITypeRef ReturnType)
{
}
