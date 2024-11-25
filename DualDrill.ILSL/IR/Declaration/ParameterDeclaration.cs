using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class ParameterDeclaration(
    string Name,
    IType Type,
    ImmutableHashSet<IShaderAttribute> Attributes) : IDeclaration
{
}

