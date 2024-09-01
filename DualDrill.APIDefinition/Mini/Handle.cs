using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public sealed record class Handle(
    string Name,
    ImmutableArray<Method> Methods,
    ImmutableArray<Property> Properties
)
{
}
