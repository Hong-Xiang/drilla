using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public sealed record class HandleDeclaration(
    string Name,
    ImmutableArray<MethodDeclaration> Methods,
    ImmutableArray<PropertyDeclaration> Properties
) : ITypeDeclaration
{
}
