namespace DualDrill.ApiGen.DrillLang.Value;

public readonly record struct IntegerValue(
    int Value,
    bool IsHexFormat = false) : IConstValue
{
}
