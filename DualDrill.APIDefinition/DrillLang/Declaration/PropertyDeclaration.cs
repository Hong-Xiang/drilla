using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Value;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class PropertyDeclaration(
    string Name,
    ITypeReference Type,
    bool IsMutable = false,
    bool IsRequired = false,
    IConstValue? DefaultValue = null
) : IDeclaration
{
}
