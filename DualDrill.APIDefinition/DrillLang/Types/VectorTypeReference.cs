namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct VectorTypeReference(Rank Size, IScalarTypeReference ElementType) : ITypeReference
{
}
