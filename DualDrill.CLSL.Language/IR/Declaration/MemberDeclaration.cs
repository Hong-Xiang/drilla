﻿using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class MemberDeclaration(
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes)
    : IDeclaration
{
    public bool Equals(MemberDeclaration? other) =>
        other is not null && Name == other.Name && Type.Equals(other.Type) && Attributes.SetEquals(other.Attributes);
}
