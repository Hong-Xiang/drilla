using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class ValueDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IType Type,
    ValueDeclarationKind Kind,
    ImmutableHashSet<CLSL.Language.IR.ShaderAttribute.IShaderAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
