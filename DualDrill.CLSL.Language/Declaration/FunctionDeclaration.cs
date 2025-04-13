using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Declaration;

[DebuggerDisplay("{DebugDisplay()}")]
public sealed class FunctionDeclaration : IDeclaration
{
    public FunctionDeclaration(string name,
        ImmutableArray<ParameterDeclaration> parameters,
        FunctionReturn @return,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        Name = name;
        Parameters = parameters;
        Return = @return;
        Attributes = attributes;
    }

    public override string ToString()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.Type.Name}"));
        return $"{Name} : ({parameters}) -> {Return.Type.Name}";
    }

    public CompoundStatement? Body { get; set; } = null;
    public string Name { get; }
    public ImmutableArray<ParameterDeclaration> Parameters { get; }
    public FunctionReturn @Return { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }

    private string DebugDisplay()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Name} : {p.Type.Name}"));
        return $"func {Return.Type.Name} {Name} ({parameters})";
    }
}

public sealed record class FunctionReturn(IShaderType Type, ImmutableHashSet<IShaderAttribute> Attributes)
{
}