using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface ILocalDeclarationContext
{
    int LabelIndex(Label label);
    int ValueIndex(IValue value);
    int VariableIndex(VariableDeclaration variable);

    ImmutableArray<VariableDeclaration> LocalVariables { get; }
    ImmutableArray<Label> Labels { get; }
    ImmutableArray<IValue> Values { get; }
}

public interface IDeclarationUser : ITextDumpable<ILocalDeclarationContext>
{
    public IEnumerable<Label> ReferencedLabels { get; }
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables { get; }
    public IEnumerable<IValue> ReferencedValues { get; }
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
        return variable.DeclarationScope == DeclarationScope.Function
            ? $"var<f> %{context.VariableIndex(variable)} : {variable.Type.Name} {variable.Name}"
            : $"var<m> {variable.Name}";
    }

    public static string ValueName(this ILocalDeclarationContext context, IValue value)
    {
        return $"let %{context.ValueIndex(value)} : {value.Type.Name}";
    }

    public static string LabelName(this ILocalDeclarationContext context, Label label) =>
        $"^{context.LabelIndex(label)} ({label.Name})";
}