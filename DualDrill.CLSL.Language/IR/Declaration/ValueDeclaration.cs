using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class ValueDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ValueDeclarationKind Kind,
    ImmutableHashSet<ShaderAttribute.IShaderAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
