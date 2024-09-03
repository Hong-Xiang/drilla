namespace DualDrill.ApiGen.DrillLang;

public readonly record struct VectorTypeReference(Rank Size, IScalarTypeReference ElementType) : ITypeReference
{
}
