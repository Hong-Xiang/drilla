using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public sealed record class EnumDeclaration(
    string Name,
    ImmutableArray<EnumValueDeclaration> Values,
    bool IsFlag = false
) : ITypeDeclaration
{
}
