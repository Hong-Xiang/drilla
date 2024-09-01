namespace DualDrill.ApiGen.Mini;

public sealed record class EnumValueDeclaration(
    string Name,
    IntegerValue Value
) : IDeclaration
{
}
