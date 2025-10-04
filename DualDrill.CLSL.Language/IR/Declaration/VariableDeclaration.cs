using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ImmutableHashSet<IAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
