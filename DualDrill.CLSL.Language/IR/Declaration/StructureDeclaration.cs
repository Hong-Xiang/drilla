using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class StructureDeclaration(
    string Name,
    ImmutableArray<MemberDeclaration> Members,
    ImmutableHashSet<IAttribute> Attributes
) : IShaderType, IDeclaration
{
}

public sealed record class MemberDeclaration(
    string Name,
    IShaderType Type,
    ImmutableHashSet<IAttribute> Attributes)
    : IDeclaration
{
    public bool Equals(MemberDeclaration? other) =>
        other is not null && Name == other.Name && Type.Equals(other.Type) && Attributes.SetEquals(other.Attributes);
}
