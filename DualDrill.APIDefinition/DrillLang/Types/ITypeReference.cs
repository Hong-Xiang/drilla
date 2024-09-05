using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.DrillLang.Types;

[JsonDerivedType(typeof(UnknownTypeReference), nameof(UnknownTypeReference))]
[JsonDerivedType(typeof(OpaqueTypeReference), nameof(OpaqueTypeReference))]
[JsonDerivedType(typeof(GenericTypeReference), nameof(GenericTypeReference))]
[JsonDerivedType(typeof(NullableTypeReference), nameof(NullableTypeReference))]
[JsonDerivedType(typeof(FutureTypeReference), nameof(FutureTypeReference))]
[JsonDerivedType(typeof(SequenceTypeReference), nameof(SequenceTypeReference))]
[JsonDerivedType(typeof(VoidTypeReference), nameof(VoidTypeReference))]
[JsonDerivedType(typeof(BoolTypeReference), nameof(BoolTypeReference))]
[JsonDerivedType(typeof(StringTypeReference), nameof(StringTypeReference))]
[JsonDerivedType(typeof(IntegerTypeReference), nameof(IntegerTypeReference))]
[JsonDerivedType(typeof(FloatTypeReference), nameof(FloatTypeReference))]
[JsonDerivedType(typeof(VectorTypeReference), nameof(VectorTypeReference))]
[JsonDerivedType(typeof(MatrixTypeReference), nameof(MatrixTypeReference))]
public interface ITypeReference
{
}

