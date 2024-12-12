using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Declaration;

[JsonDerivedType(typeof(FunctionDeclaration), nameof(FunctionDeclaration))]
[JsonDerivedType(typeof(ParameterDeclaration), nameof(ParameterDeclaration))]
[JsonDerivedType(typeof(StructureDeclaration), nameof(StructureDeclaration))]
[JsonDerivedType(typeof(MemberDeclaration), nameof(MemberDeclaration))]
//TODO: find property of serialization of IType
//[JsonDerivedType(typeof(IType), nameof(IType))]
//[JsonDerivedType(typeof(TypeDeclaration), nameof(TypeDeclaration))]
[JsonDerivedType(typeof(ValueDeclaration), nameof(ValueDeclaration))]
public interface IDeclaration : IShaderAstNode
{
    string Name { get; }
    ImmutableHashSet<IShaderAttribute> Attributes { get; }
}
