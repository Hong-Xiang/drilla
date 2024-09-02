namespace DualDrill.ApiGen.Mini;

public readonly record struct VectorTypeReference(Rank Size, IScalarTypeReference ElementType) : ITypeReference
{
}
