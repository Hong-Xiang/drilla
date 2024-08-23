using System.Collections.Immutable;

namespace ILSLPrototype;

public interface IWGSLSyntax
{
}


sealed record class AttributeRef(
    string Name,
    ImmutableArray<string> ParamterValues
)
{
}

sealed record class FieldDecl(
    string Name,
    ImmutableArray<AttributeRef> Attributes,
    TypeRef Type
)
{
}

sealed record class StrutDecl(
    string Name,
    ImmutableArray<FieldDecl> Fields
)
{
}

sealed record class ParameterDecl(
    string Name,
    TypeRef Type,
    ImmutableArray<AttributeRef> Attributes
)
{ }

sealed record class TypeRef(string Name)
{
}

sealed record class MethodDecl(
    string Name,
    ImmutableArray<AttributeRef> Attributes,
    ImmutableArray<ParameterDecl> Parameters,
    TypeRef ReturnType,
    ImmutableArray<AttributeRef> ReturnAttribute
);
