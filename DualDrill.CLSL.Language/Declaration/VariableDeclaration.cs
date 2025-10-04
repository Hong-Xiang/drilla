using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class VariableDeclaration
    : IDeclaration
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