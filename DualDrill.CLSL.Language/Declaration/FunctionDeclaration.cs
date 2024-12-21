using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Declaration;

[DebuggerDisplay("{DebugDisplay()}")]
public sealed record class FunctionDeclaration(
    string Name,
    ImmutableArray<ParameterDeclaration> Parameters,
    FunctionReturn Return,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration
{
    public CompoundStatement? Body { get; set; } = null;

    private string DebugDisplay()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Name} : {p.Type.Name}"));
        return $"func {Return.Type.Name} {Name} ({parameters})";
    }
}

public sealed record class FunctionReturn(IShaderType Type, ImmutableHashSet<IShaderAttribute> Attributes)
{
}
