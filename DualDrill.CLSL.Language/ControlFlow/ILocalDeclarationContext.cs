using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface ILocalDeclarationContext
{
    int LabelIndex(Label label);
    int VariableIndex(VariableDeclaration variable);
}

public sealed class LocalDeclarationContext(
    ImmutableArray<Label> Labels,
    ImmutableArray<VariableDeclaration> Variables
) : ILocalDeclarationContext
{
    private FrozenDictionary<Label, int> LabelIndexLookup { get; } =
        Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    private FrozenDictionary<VariableDeclaration, int> VariableIndexLookup { get; } =
        Variables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    public int LabelIndex(Label label) => LabelIndexLookup[label];
    public int VariableIndex(VariableDeclaration variable) => VariableIndexLookup[variable];

    public static LocalDeclarationContext Empty { get; } = new([], []);
}