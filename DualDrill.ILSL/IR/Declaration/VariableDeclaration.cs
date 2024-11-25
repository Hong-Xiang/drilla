using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IType Type,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration
{
    public IExpression? Initializer { get; set; } = null;
}
