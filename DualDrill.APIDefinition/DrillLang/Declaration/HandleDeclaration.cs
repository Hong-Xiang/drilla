using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class HandleDeclaration(
    string Name,
    ImmutableArray<MethodDeclaration> Methods,
    ImmutableArray<PropertyDeclaration> Properties
) : ITypeDeclaration
{
}
