using DualDrill.ApiGen.DrillLang.Value;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class EnumMemberDeclaration(
    string Name,
    IntegerValue? Value = null
) : IDeclaration
{
}
