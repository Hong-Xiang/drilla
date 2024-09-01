namespace DualDrill.ApiGen.Mini;

public sealed record class ParameterDeclaration(
    string Name,
    ITypeReference Type,
    IConstValue? DefaultValue) : IDeclaration
{
}
