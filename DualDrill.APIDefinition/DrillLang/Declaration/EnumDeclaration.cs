using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class EnumDeclaration(
    string Name,
    ImmutableArray<EnumMemberDeclaration> Values,
    bool IsFlag = false
) : ITypeDeclaration
{
}
