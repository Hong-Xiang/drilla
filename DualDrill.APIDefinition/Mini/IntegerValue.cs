namespace DualDrill.ApiGen.Mini;

public sealed record class IntegerValue(long Value, bool IsHexFormat = false) : IConstValue
{
}
