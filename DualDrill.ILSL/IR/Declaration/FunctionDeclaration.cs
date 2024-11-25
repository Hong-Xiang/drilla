using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.ILSL.IR.Statement;
using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class FunctionDeclaration(
    string Name,
    ImmutableArray<ParameterDeclaration> Parameters,
    FunctionReturn Return,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration
{
    public CompoundStatement? Body { get; set; } = null;
}

public readonly record struct FunctionReturn(IType? Type, ImmutableHashSet<IShaderAttribute> Attributes)
{
}
