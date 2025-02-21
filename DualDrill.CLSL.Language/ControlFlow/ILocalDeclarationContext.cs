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

public sealed class LocalDeclarationContext : ILocalDeclarationContext
{
    public LocalDeclarationContext(IEnumerable<Label> labels,
        IEnumerable<VariableDeclaration> localVariables)
    {
        Labels = [..labels.Distinct()];
        LocalVariables = [..localVariables.Distinct()];

        LabelIndexLookup = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        VariableIndexLookup = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
    }

    public ImmutableArray<Label> Labels { get; }
    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    private FrozenDictionary<Label, int> LabelIndexLookup { get; }

    private FrozenDictionary<VariableDeclaration, int> VariableIndexLookup { get; }

    public int LabelIndex(Label label) => LabelIndexLookup[label];
    public int VariableIndex(VariableDeclaration variable) => VariableIndexLookup[variable];

    public static LocalDeclarationContext Empty { get; } = new([], []);
}