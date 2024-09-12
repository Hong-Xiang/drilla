using DualDrill.ILSL.IR.Expression;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    IName Name,
    IType Type,
    ImmutableHashSet<IAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
