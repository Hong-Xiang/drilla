using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.Mini;

[JsonDerivedType(typeof(UnknownTypeRef), "unknown-type")]
[JsonDerivedType(typeof(PlainTypeRef), "plain")]
[JsonDerivedType(typeof(GenericTypeRef), "generic")]
[JsonDerivedType(typeof(NullableTypeRef), "nullable")]
[JsonDerivedType(typeof(FutureTypeRef), "future")]
[JsonDerivedType(typeof(SequenceTypeRef), "sequence")]
[JsonDerivedType(typeof(VoidTypeRef), "void")]
public interface ITypeRef
{
}
