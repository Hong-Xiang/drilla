﻿using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class FormalParameterExpression(ParameterDeclaration Parameter) : IExpression
{
    public IShaderType Type => Parameter.Type;
}
