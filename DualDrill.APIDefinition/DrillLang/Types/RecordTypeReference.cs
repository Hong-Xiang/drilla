namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct RecordTypeReference(
    ITypeReference KeyType,
    ITypeReference ValueType
) : ITypeReference
{
}
