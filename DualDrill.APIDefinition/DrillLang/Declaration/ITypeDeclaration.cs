using System.Text.Json.Serialization;

namespace DualDrill.ApiGen.DrillLang.Declaration;

[JsonDerivedType(typeof(StructDeclaration), nameof(StructDeclaration))]
[JsonDerivedType(typeof(HandleDeclaration), nameof(HandleDeclaration))]
[JsonDerivedType(typeof(EnumDeclaration), nameof(EnumDeclaration))]
[JsonDerivedType(typeof(UnknownTypeDeclaration), nameof(UnknownTypeDeclaration))]
public interface ITypeDeclaration : IDeclaration
{
}
