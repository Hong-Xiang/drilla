using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class ParameterDeclaration
    : IDeclaration, IVariableIdentifierSymbol
{
    public ParameterDeclaration(string name,
        IShaderType type,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        Name = name;
        Type = type;
        Attributes = attributes;
        Value = new(this);
    }

    public override string ToString() => $"arg({Name}: {Type.Name})";

    public T Evaluate<T>(IDeclarationSemantic<T> semantic)
        => semantic.VisitParameter(this);

    public string Name { get; }
    public IShaderType Type { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }

    public ParameterPointerValue Value { get; }
}


public class ParameterPointerValue : IShaderValue
{
    public ParameterDeclaration Declaration { get; }

    public IShaderType Type => Declaration.Type.GetPtrType();

    internal ParameterPointerValue(ParameterDeclaration declaration)
    {
        Declaration = declaration;
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}
