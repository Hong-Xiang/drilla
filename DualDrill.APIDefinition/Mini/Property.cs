namespace DualDrill.ApiGen.Mini;

public sealed record class Property(
    string Name,
    ITypeRef Type,
    bool IsMutable = false
)
{
}
