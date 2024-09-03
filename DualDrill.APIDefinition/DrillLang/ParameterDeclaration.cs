namespace DualDrill.ApiGen.DrillLang;

public sealed record class ParameterDeclaration(
    string Name,
    ITypeReference Type,
    IConstValue? DefaultValue) : IDeclaration
{
}
