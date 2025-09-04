using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

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
        Value = new(this);
    }

    public IExpression? Initializer { get; set; } = null;

    public T Evaluate<T>(IDeclarationSemantic<T> semantic)
        => semantic.VisitVariable(this);

    public override string ToString()
    {
        return $"var@{AddressSpace}({Name}: {Type.Name})";
    }

    public VariablePointerValue Value { get; }
    public IAddressSpace AddressSpace { get; }
    public string Name { get; }
    public IShaderType Type { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }
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
    public VariableDeclaration Declaration { get; }
    public IShaderType Type => Declaration.Type.GetPtrType(Declaration.AddressSpace);
    internal VariablePointerValue(VariableDeclaration declaration)
    {
        Declaration = declaration;
    }
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}
