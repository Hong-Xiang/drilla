namespace DualDrill.ApiGen.DrillLang;

public sealed record class PropertyDeclaration(
    string Name,
    ITypeReference Type,
    bool IsMutable = false
) : IDeclaration
{
}
