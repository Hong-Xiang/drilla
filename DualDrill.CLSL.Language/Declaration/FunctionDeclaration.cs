using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;

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

    public override string ToString()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Name}: {p.Type.Name}"));
        return $"func {Name} : ({parameters}) -> {Return.Type.Name}";
    }

    public CompoundStatement? Body { get; set; } = null;
    public string Name { get; }
    public ImmutableArray<ParameterDeclaration> Parameters { get; }
    public FunctionReturn Return { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }

    public IShaderType Type { get; }

    private string DebugDisplay()
        => ToString();

    public T Evaluate<T>(IDeclarationSemantic<T> semantic)
        => semantic.VisitFunction(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}

public sealed record class FunctionReturn(IShaderType Type, ImmutableHashSet<IShaderAttribute> Attributes)
{
}

