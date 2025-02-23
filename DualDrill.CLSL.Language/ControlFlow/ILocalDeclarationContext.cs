using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface ILocalDeclarationContext
{
    int LabelIndex(Label label);
    int VariableIndex(VariableDeclaration variable);

    ImmutableArray<VariableDeclaration> LocalVariables { get; }
    ImmutableArray<Label> Labels { get; }
}

public interface ILocalDeclarationReferencingElement
{
    public IEnumerable<Label> ReferencedLabels { get; }
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables { get; }
}

public static class LocalDeclarationContextExtensions
{
    public static string VariableName(this ILocalDeclarationContext context, VariableDeclaration variable)
    {
        return variable.DeclarationScope == DeclarationScope.Function
            ? $"var%{context.VariableIndex(variable)} {variable}"
            : $"module var {variable.Name}";
    }

    public static string LabelName(this ILocalDeclarationContext context, Label label) =>
        $"label%{context.LabelIndex(label)} {label}";
}

public sealed class LocalDeclarationContext : ILocalDeclarationContext
{
    public LocalDeclarationContext(IEnumerable<ILocalDeclarationReferencingElement> elements)
    {
        ImmutableArray<ILocalDeclarationReferencingElement> els = [..elements];
        Labels = [..els.SelectMany(e => e.ReferencedLabels).Distinct()];
        LocalVariables = [..els.SelectMany(e => e.ReferencedLocalVariables).Distinct()];

        LabelIndexLookup = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        VariableIndexLookup = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public ImmutableArray<Label> Labels { get; }
    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    private FrozenDictionary<Label, int> LabelIndexLookup { get; }

    private FrozenDictionary<VariableDeclaration, int> VariableIndexLookup { get; }

    public int LabelIndex(Label label) => LabelIndexLookup[label];
    public int VariableIndex(VariableDeclaration variable) => VariableIndexLookup[variable];

    public static LocalDeclarationContext Empty { get; } = new([]);
}