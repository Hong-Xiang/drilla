using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class ParameterDeclaration : IDeclaration, IVariableIdentifierSymbol
{
    public ParameterDeclaration(string name,
        IShaderType type,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        Name = name;
        Type = type;
        Attributes = attributes;
    }

    public override string ToString() => $"arg({Name}: {Type.Name})";

    public string Name { get; }
    public IShaderType Type { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }
}

public interface IParameterSymbol : ISymbol
{
    IFunctionSymbol Function { get; }
    int Index { get; }
    bool IsThis { get; }
}