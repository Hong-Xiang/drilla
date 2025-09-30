using System.Diagnostics.CodeAnalysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Compiler;

/// <summary>
///     ControlFlowGraphBuilder build control flow graph from linear instructions with control flow instructions like
///     br, br.if, switch, return, etc.
///     When building nodes of control flow graph, basis blocks could be constructed
/// </summary>
public sealed class ControlFlowGraphBuilder
{
    private readonly Dictionary<int, ISuccessor> IndexSuccessors = [];

    public ControlFlowGraphBuilder(int totalInstructionCount, Func<int, Label> instructionIndexToLabelFactory)
    {
        if (!(totalInstructionCount >= 1))
            throw new ArgumentException($"instruction count >= 1 is required, got {totalInstructionCount}");
        CreateLabelFromIndex = instructionIndexToLabelFactory;
        TotalInstructionCount = totalInstructionCount;
        AddLabel(0, instructionIndexToLabelFactory(0));
    }

    public Label Entry => IndexToLabel[0];

    private Dictionary<int, Label> IndexToLabel { get; } = [];
    private Dictionary<Label, int> LabelToIndex { get; } = [];

    private int TotalInstructionCount { get; }
    private Func<int, Label> CreateLabelFromIndex { get; }

    /// <summary>
    ///     Get label starts with instruction of index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Label this[int index] => IndexToLabel[index];

    private void AddLabel(int index, Label label)
    {
        IndexToLabel.Add(index, label);
        LabelToIndex.Add(label, index);
    }

    private Label GetOrCreateLabel(int index)
    {
        if (IndexToLabel.TryGetValue(index, out var result)) return result;

        var label = CreateLabelFromIndex(index);
        AddLabel(index, label);
        return label;
    }

    private bool TryGetOrCreateLabel(int index, [NotNullWhen(true)] out Label? result)
    {
        if (index >= TotalInstructionCount)
        {
            result = default;
            return false;
        }

        result = GetOrCreateLabel(index);
        return true;
    }


    public Label AddBr(int source, int target)
    {
        var targetLabel = GetOrCreateLabel(target);
        IndexSuccessors.Add(source, Successor.Unconditional(targetLabel));
        _ = TryGetOrCreateLabel(source + 1, out _);
        return targetLabel;
    }

    public Label AddBrIf(int source, int target)
    {
        var trueLabel = GetOrCreateLabel(target);
        var falseLabel = GetOrCreateLabel(source + 1);
        IndexSuccessors.Add(source, Successor.Conditional(trueLabel, falseLabel));
        return trueLabel;
    }

    public void AddReturn(int source)
    {
        IndexSuccessors.Add(source, new TerminateSuccessor());
        _ = TryGetOrCreateLabel(source + 1, out _);
    }

    public ControlFlowGraph<TNode> Build<TNode>(Func<Label, InstructionRange, TNode> createNode)
    {
        Dictionary<Label, int> labelInstructionCount = [];
        var indexToLabel = new Label[TotalInstructionCount];
        Dictionary<Label, ISuccessor> labelSuccessors = [];

        // scan instructions to get following maps
        // label -> instructionCount
        // index -> label of containing range
        // label -> successor
        Label? current = default;
        for (var i = 0; i < TotalInstructionCount; i++)
        {
            if (IndexToLabel.TryGetValue(i, out var next)) current = next;
            if (current is null) throw new NullReferenceException("Can not find associated label");
            labelInstructionCount.TryAdd(current, 0);
            labelInstructionCount[current]++;
            indexToLabel[i] = current;
            if (IndexSuccessors.TryGetValue(i, out var s)) labelSuccessors.Add(current, s);
        }

        // handling normal fall through suceessors
        foreach (var (label, idx) in LabelToIndex)
            if (!labelSuccessors.ContainsKey(label))
            {
                var nextInst = idx + labelInstructionCount[label];
                if (nextInst >= TotalInstructionCount - 1)
                    labelSuccessors.Add(label, Successor.Terminate());
                else
                    labelSuccessors.Add(label, Successor.Unconditional(indexToLabel[nextInst]));
            }

        var nodes = labelInstructionCount.Select(kv =>
        {
            var range = new InstructionRange(LabelToIndex[kv.Key], labelInstructionCount[kv.Key]);
            var node = createNode(kv.Key, range);
            return KeyValuePair.Create(kv.Key,
                new ControlFlowGraph<TNode>.NodeDefinition(labelSuccessors[kv.Key], node));
        }).ToDictionary();

        return new ControlFlowGraph<TNode>(
            Entry,
            nodes
        );
    }

    public readonly record struct InstructionRange(int Start, int Count)
    {
    }
}