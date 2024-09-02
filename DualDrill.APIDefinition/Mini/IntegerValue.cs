namespace DualDrill.ApiGen.Mini;

public readonly record struct IntegerValue(
    int Value,
    bool IsHexFormat = false) : IConstValue
{
}
