using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class VariableDeclaration
    : IDeclaration, IVariableIdentifierSymbol
{
    public VariableDeclaration(
        IAddressSpace addressSpace,
        string name,
        IShaderType type,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        AddressSpace = addressSpace;
        Name = name;
        Type = type;
        Attributes = attributes;
        Value = new VariablePointerValue(this);
    }

    public VariablePointerValue Value { get; }
    public IAddressSpace AddressSpace { get; }

    public T Evaluate<T>(IDeclarationSemantic<T> semantic) => semantic.VisitVariable(this);

    public string Name { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }
    public IShaderType Type { get; }

    public override string ToString() => $"var@{AddressSpace}({Name}: {Type.Name})";
}

public sealed record class LocalVariableDeclaration(
    string? Name,
    IShaderType Type,
    IReadOnlyList<IShaderAttribute> Attributes
)
{
}

public interface IModuleVariableSymbol : ISymbol
{
    IShaderType Type { get; }
}

public interface ILocalVariableSymbol : ISymbol
{
    IShaderType Type { get; }
}

public class VariablePointerValue : IShaderValue
{
    internal VariablePointerValue(VariableDeclaration declaration)
    {
        Declaration = declaration;
    }

    public VariableDeclaration Declaration { get; }
    public IShaderType Type => Declaration.Type.GetPtrType(Declaration.AddressSpace);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}