namespace DualDrill.ApiGen.DrillLang;

public readonly record struct IntegerTypeReference(
    BitWidth BitWidth,
    bool Signed) : IScalarTypeReference
{
}
