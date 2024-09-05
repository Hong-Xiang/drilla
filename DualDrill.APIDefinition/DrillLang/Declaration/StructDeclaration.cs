using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class StructDeclaration(
    string Name,
    ImmutableArray<PropertyDeclaration> Properties
) : ITypeDeclaration
{
}
