using DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

public sealed record class FunctionDeclaration(
    string Name,
    ImmutableArray<ParameterDeclaration> Parameters,
    FunctionReturn Return,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration
{
    public CompoundStatement? Body { get; set; } = null;
}

public sealed record class FunctionReturn(IShaderType Type, ImmutableHashSet<IShaderAttribute> Attributes)
{
}
