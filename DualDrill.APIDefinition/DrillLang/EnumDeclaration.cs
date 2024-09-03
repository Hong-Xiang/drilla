using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang;

public sealed record class EnumDeclaration(
    string Name,
    ImmutableArray<EnumValueDeclaration> Values,
    bool IsFlag = false
) : ITypeDeclaration
{
}
