using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class ParameterDeclaration
    : IDeclaration
{
    public ParameterDeclaration(
        string name,
        IShaderType type,
        ImmutableHashSet<IShaderAttribute> attributes)
    {
        Name = name;
        Type = type;
        Attributes = attributes;
        Value = new ParameterPointerValue(this);
    }

    public ParameterPointerValue Value { get; }

    public T Evaluate<T>(IDeclarationSemantic<T> semantic) => semantic.VisitParameter(this);

    public string Name { get; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; }
    public IShaderType Type { get; }

    public override string ToString() => $"arg({Name}: {Type.Name})";
}

public class ParameterPointerValue : IShaderValue
{
    internal ParameterPointerValue(ParameterDeclaration declaration)
    {
        Declaration = declaration;
        var hasSemanticBinding = Declaration.Attributes.Any(a => a is ISemanticBindingAttribute);
        Type = Declaration.Type.GetPtrType(hasSemanticBinding
            ? InputAddressSpace.Instance
            : FunctionAddressSpace.Instance);
    }

    public ParameterDeclaration Declaration { get; }

    public IShaderType Type { get; }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}