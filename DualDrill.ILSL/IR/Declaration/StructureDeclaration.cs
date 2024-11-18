using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class StructureDeclaration(
    string Name,
    ImmutableArray<MemberDeclaration> Members,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IType, IDeclaration
{
}

public sealed record class MemberDeclaration(
    string Name,
    IType Type,
    ImmutableHashSet<IShaderAttribute> Attributes)
    : IDeclaration
{
    public bool Equals(MemberDeclaration? other) =>
        other is not null && Name == other.Name && Type.Equals(other.Type) && Attributes.SetEquals(other.Attributes);
}
