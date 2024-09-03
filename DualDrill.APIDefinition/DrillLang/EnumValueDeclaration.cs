namespace DualDrill.ApiGen.DrillLang;

public sealed record class EnumValueDeclaration(
    string Name,
    IntegerValue Value
) : IDeclaration
{
}
