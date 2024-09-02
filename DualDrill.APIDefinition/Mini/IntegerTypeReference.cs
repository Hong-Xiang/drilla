namespace DualDrill.ApiGen.Mini;

public readonly record struct IntegerTypeReference(
    BitWidth BitWidth,
    bool Signed) : IScalarTypeReference
{
}
