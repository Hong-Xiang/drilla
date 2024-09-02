namespace DualDrill.ApiGen.Mini;

public sealed record class PropertyDeclaration(
    string Name,
    ITypeReference Type,
    bool IsMutable = false
) : IDeclaration
{
}
