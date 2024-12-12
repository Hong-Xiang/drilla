using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

public sealed record class ValueDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ValueDeclarationKind Kind,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
