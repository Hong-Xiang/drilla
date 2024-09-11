using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Value;

namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class ParameterDeclaration(
    string Name,
    ITypeReference Type,
    IConstValue? DefaultValue) : IDeclaration
{
}
