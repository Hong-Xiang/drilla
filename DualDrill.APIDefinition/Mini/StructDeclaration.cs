using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public sealed record class StructDeclaration(
    string Name,
    ImmutableArray<PropertyDeclaration> Properties
) : ITypeDeclaration
{
}
