namespace DualDrill.ApiGen.Mini;

public readonly record struct MatrixTypeReference(Rank Row, Rank Col, FloatTypeReference ElementType) : ITypeReference
{
}
