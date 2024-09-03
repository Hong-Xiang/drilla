namespace DualDrill.ApiGen.DrillLang;

public readonly record struct IntegerValue(
    int Value,
    bool IsHexFormat = false) : IConstValue
{
}
