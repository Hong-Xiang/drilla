using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface ILocalDeclarationContext
{
    int LabelIndex(Label label);
    int ValueIndex(IShaderValue value);
    // TODO: add type index
    // int TypeIndex(IShaderType type);


    // Remove following things
    int VariableIndex(VariableDeclaration variable);

    ImmutableArray<VariableDeclaration> LocalVariables { get; }
    ImmutableArray<Label> Labels { get; }
    ImmutableArray<IShaderValue> Values { get; }
}


public interface IDeclarationUser : ITextDumpable<ILocalDeclarationContext>
{
    public IEnumerable<Label> ReferencedLabels { get; }
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables { get; }
    public IEnumerable<IShaderValue> ReferencedValues { get; }
}

public static class LocalDeclarationContextExtensions
{
    public static string Dump(this IDeclarationUser self)
    {
        var context = new LocalDeclarationContext([self]);
        return self.Dump(context);
    }

    public static string VariableName(this ILocalDeclarationContext context, VariableDeclaration variable)
    {
        return variable.AddressSpace is FunctionAddressSpace
            ? $"var<f> %{context.VariableIndex(variable)} : {variable.Type.Name} {variable.Name}"
            : $"var<m> {variable.Name}";
    }

    public static string ValueName(this ILocalDeclarationContext context, IShaderValue value)
    {
        return $"let %{context.ValueIndex(value)} : {value.Type.Name}";
    }

    public static string LabelName(this ILocalDeclarationContext context, Label label) =>
        $"^{context.LabelIndex(label)} ({label.Name})";

    public static string LabelName2(this ILocalDeclarationContext context, Label label) =>
        $"^{context.LabelIndex(label)}:{label.Name}";
}