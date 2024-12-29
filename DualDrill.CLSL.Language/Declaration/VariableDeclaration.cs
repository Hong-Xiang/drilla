using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    int LocalIndex,
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration, IVariableIdentifierResolveResult
{
    public IExpression? Initializer { get; set; } = null;
}
