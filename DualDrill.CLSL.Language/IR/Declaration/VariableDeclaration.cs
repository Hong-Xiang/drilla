using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration, IVariableIdentifierResolveResult
{
    public IExpression? Initializer { get; set; } = null;
}
