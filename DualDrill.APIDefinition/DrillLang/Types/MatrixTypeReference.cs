namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct MatrixTypeReference(Rank Row, Rank Col, FloatTypeReference ElementType) : ITypeReference
{
}
