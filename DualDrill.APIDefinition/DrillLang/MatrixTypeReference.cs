namespace DualDrill.ApiGen.DrillLang;

public readonly record struct MatrixTypeReference(Rank Row, Rank Col, FloatTypeReference ElementType) : ITypeReference
{
}
