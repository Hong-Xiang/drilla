namespace DualDrill.ApiGen.Mini;

public sealed record class Parameter(
    string Name,
    ITypeRef Type,
    IConstValue? DefaultValue)
{
}
