using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

[DebuggerDisplay("{DebugDisplay()}")]
public sealed class FunctionDeclaration
    : IDeclaration, IShaderValue
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
        Type = new FunctionType([.. parameters.Select(p => p.Type)], @return.Type);
    }

    public ImmutableArray<ParameterDeclaration> Parameters { get; }
    public FunctionReturn Return { get; }
    public string Name { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }

    public T Evaluate<T>(IDeclarationSemantic<T> semantic) => semantic.VisitFunction(this);

    public IShaderType Type { get; }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.Type.Name}"));
        return $"func {Name} : ({parameters}) -> {Return.Type.Name}";
    }

    private string DebugDisplay() => ToString();
}

public sealed record class FunctionReturn(IShaderType Type, ImmutableHashSet<IShaderAttribute> Attributes)
{
}