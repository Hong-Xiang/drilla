using DualDrill.ILSL.IR.Expression;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class ValueDeclaration(
    DeclarationScope DeclarationScope,
    IName Name,
    IType Type,
    ValueDeclarationKind Kind,
    ImmutableHashSet<IAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
