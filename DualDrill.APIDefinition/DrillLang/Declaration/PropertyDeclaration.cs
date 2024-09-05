using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class PropertyDeclaration(
    string Name,
    ITypeReference Type,
    bool IsMutable = false
) : IDeclaration
{
}
