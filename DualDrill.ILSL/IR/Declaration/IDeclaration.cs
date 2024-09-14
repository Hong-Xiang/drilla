using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DualDrill.ILSL.IR.Declaration;

[JsonDerivedType(typeof(FunctionDeclaration), nameof(FunctionDeclaration))]
[JsonDerivedType(typeof(ParameterDeclaration), nameof(ParameterDeclaration))]
[JsonDerivedType(typeof(TypeDeclaration), nameof(TypeDeclaration))]
[JsonDerivedType(typeof(ValueDeclaration), nameof(ValueDeclaration))]
public interface IDeclaration : INode
{
    string Name { get; }
    ImmutableHashSet<IAttribute> Attributes { get; }
}
