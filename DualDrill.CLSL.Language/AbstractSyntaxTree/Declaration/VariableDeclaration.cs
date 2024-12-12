using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration, IVariableIdentifierResolveResult
{
    public IExpression? Initializer { get; set; } = null;
}
