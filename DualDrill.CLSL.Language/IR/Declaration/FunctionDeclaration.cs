﻿using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class FunctionDeclaration(
    string Name,
    ImmutableArray<ParameterDeclaration> Parameters,
    FunctionReturn Return,
    ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes
) : IDeclaration
{
    public CompoundStatement? Body { get; set; } = null;
}

public sealed record class FunctionReturn(IShaderType Type, ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes)
{
}
