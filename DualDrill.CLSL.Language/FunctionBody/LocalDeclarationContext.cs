using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed class LocalDeclarationContext : ILocalDeclarationContext
{
    public LocalDeclarationContext(
        IEnumerable<IDeclarationUser> elements
    )
    {
        ImmutableArray<IDeclarationUser> els = [..elements];
        Labels = [..els.SelectMany(e => e.ReferencedLabels).Distinct()];
        LocalVariables = [..els.SelectMany(e => e.ReferencedLocalVariables).Distinct()];
        Values = [..els.SelectMany(e => e.ReferencedValues).Distinct()];
        LabelIndices = Labels.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        LocalVariableIndices = LocalVariables.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        ValueIndices = Values.Index().ToFrozenDictionary(x => x.Item, x => x.Index + LocalVariableIndices.Count);
    }


    public int LabelIndex(Label label)
        => LabelIndices[label];

    public int VariableIndex(VariableDeclaration variable)
        => LocalVariableIndices[variable];

    public int ValueIndex(IValue value)
        => ValueIndices[value];

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
    public ImmutableArray<IValue> Values { get; }

    private readonly FrozenDictionary<Label, int> LabelIndices;
    private readonly FrozenDictionary<VariableDeclaration, int> LocalVariableIndices;
    private readonly FrozenDictionary<IValue, int> ValueIndices;
}