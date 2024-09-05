namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct IntegerTypeReference(
    BitWidth BitWidth,
    bool Signed) : IScalarTypeReference
{
}
