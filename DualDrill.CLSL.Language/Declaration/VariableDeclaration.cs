using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed record class VariableDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes)
    : IDeclaration, IVariableIdentifierSymbol
{
    public IExpression? Initializer { get; set; } = null;

    public override string ToString()
    {
        var scope = DeclarationScope switch
        {
            DeclarationScope.Module => "m",
            DeclarationScope.Function => "f",
            _ => throw new NotSupportedException()
        };
        return $"var@{scope}({Name}: {Type.Name})";
    }
}

public sealed record class LocalVariableDeclaration(
    string? Name,
    IShaderType Type,
    IReadOnlyList<IShaderAttribute> Attributes
)
{
}