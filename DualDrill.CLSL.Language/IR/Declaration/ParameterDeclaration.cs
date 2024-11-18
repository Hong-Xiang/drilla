﻿using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class ParameterDeclaration(
    string Name,
    IShaderType Type,
    ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes) : IDeclaration
{
}

