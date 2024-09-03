using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.DrillLang;

[JsonDerivedType(typeof(UnknownTypeRef), nameof(UnknownTypeRef))]
[JsonDerivedType(typeof(PlainTypeRef), nameof(PlainTypeRef))]
[JsonDerivedType(typeof(GenericTypeRef), nameof(GenericTypeRef))]
[JsonDerivedType(typeof(NullableTypeRef), nameof(NullableTypeRef))]
[JsonDerivedType(typeof(FutureTypeRef), nameof(FutureTypeRef))]
[JsonDerivedType(typeof(SequenceTypeRef), nameof(SequenceTypeRef))]
[JsonDerivedType(typeof(VoidTypeRef), nameof(VoidTypeRef))]
[JsonDerivedType(typeof(BoolTypeReference), nameof(BoolTypeReference))]
[JsonDerivedType(typeof(StringTypeReference), nameof(StringTypeReference))]
[JsonDerivedType(typeof(IntegerTypeReference), nameof(IntegerTypeReference))]
[JsonDerivedType(typeof(FloatTypeReference), nameof(FloatTypeReference))]
[JsonDerivedType(typeof(VectorTypeReference), nameof(VectorTypeReference))]
[JsonDerivedType(typeof(MatrixTypeReference), nameof(MatrixTypeReference))]
public interface ITypeReference
{
}

[JsonDerivedType(typeof(StructDeclaration), nameof(StructDeclaration))]
[JsonDerivedType(typeof(HandleDeclaration), nameof(HandleDeclaration))]
[JsonDerivedType(typeof(EnumDeclaration), nameof(EnumDeclaration))]
public interface ITypeDeclaration : IDeclaration
{
}
