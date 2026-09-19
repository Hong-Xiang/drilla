using System.CodeDom.Compiler;
using System.Diagnostics.CodeAnalysis;
using DualDrill.CLSL.Language;
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
        ValidateInstructionIndex(index, nameof(index));
        if (IndexToLabel.TryGetValue(index, out var result)) return result;

        var label = CreateLabelFromIndex(index);
        AddLabel(index, label);
        return label;
    }

    private bool TryGetOrCreateLabel(int index, [NotNullWhen(true)] out Label? result)
    {
        if (index < 0 || index >= TotalInstructionCount)
        {
            result = default;
            return false;
        }

        result = GetOrCreateLabel(index);
        return true;
    }


    public Label AddBr(int source, int target)
    {
        ValidateInstructionIndex(source, nameof(source));
        ValidateInstructionIndex(target, nameof(target));
        var targetLabel = GetOrCreateLabel(target);
        IndexSuccessors.Add(source, Successor.Unconditional(targetLabel));
        _ = TryGetOrCreateLabel(source + 1, out _);
        return targetLabel;
    }

    public Label AddBrIf(int source, int target)
    {
        ValidateInstructionIndex(source, nameof(source));
        ValidateInstructionIndex(target, nameof(target));
        if (source + 1 >= TotalInstructionCount)
            throw new ArgumentOutOfRangeException(nameof(source),
                "A conditional branch must have a fallthrough instruction.");

        var trueLabel = GetOrCreateLabel(target);
        var falseLabel = GetOrCreateLabel(source + 1);
        IndexSuccessors.Add(source, Successor.Conditional(trueLabel, falseLabel));
        return trueLabel;
    }

    public void AddReturn(int source)
    {
        ValidateInstructionIndex(source, nameof(source));
        IndexSuccessors.Add(source, new TerminateSuccessor());
        _ = TryGetOrCreateLabel(source + 1, out _);
    }

    public ControlFlowGraph<TNode> Build<TNode>(
        Func<Label, InstructionRange, ISuccessor, TNode> createNode,
        Func<TNode, ISuccessor> getSuccessor,
        Action<ControlFlowGraph<TNode>, IndentedTextWriter, PrettyPrintOption>? prettyPrint = null)
    {
        return BuildReachable(
            Enumerable.Range(0, TotalInstructionCount).ToHashSet(),
            createNode,
            getSuccessor,
            prettyPrint);
    }

    public ControlFlowGraph<TNode> BuildReachable<TNode>(
        IReadOnlySet<int> reachableInstructionIndices,
        Func<Label, InstructionRange, ISuccessor, TNode> createNode,
        Func<TNode, ISuccessor> getSuccessor,
        Action<ControlFlowGraph<TNode>, IndentedTextWriter, PrettyPrintOption>? prettyPrint = null)
    {
        if (!reachableInstructionIndices.Contains(0))
            throw new ArgumentException("The reachable instruction set must contain the entry instruction.",
                nameof(reachableInstructionIndices));

        foreach (var index in reachableInstructionIndices)
            ValidateInstructionIndex(index, nameof(reachableInstructionIndices));

        Dictionary<Label, int> labelInstructionCount = [];
        Dictionary<int, Label> indexToLabel = [];
        Dictionary<Label, ISuccessor> labelSuccessors = [];

        Label? current = default;
        int? previous = null;
        foreach (var index in reachableInstructionIndices.Order())
        {
            if (previous is null || index != previous + 1)
                current = GetOrCreateLabel(index);
            else if (IndexToLabel.TryGetValue(index, out var next))
                current = next;

            if (current is null)
                throw new InvalidOperationException($"Cannot find a basic-block label for instruction {index}.");

            labelInstructionCount.TryAdd(current, 0);
            labelInstructionCount[current]++;
            indexToLabel[index] = current;
            if (IndexSuccessors.TryGetValue(index, out var successor))
            {
                foreach (var target in successor.AllTargets())
                {
                    var targetIndex = LabelToIndex[target];
                    if (!reachableInstructionIndices.Contains(targetIndex))
                        throw new InvalidOperationException(
                            $"Reachable instruction {index} targets unreachable instruction {targetIndex}.");
                }

                labelSuccessors.Add(current, successor);
            }

            previous = index;
        }

        foreach (var (label, count) in labelInstructionCount)
        {
            if (labelSuccessors.ContainsKey(label))
                continue;

            var start = LabelToIndex[label];
            var nextInstruction = start + count;
            if (nextInstruction == TotalInstructionCount)
            {
                labelSuccessors.Add(label, Successor.Terminate());
                continue;
            }

            if (!reachableInstructionIndices.Contains(nextInstruction))
                throw new InvalidOperationException(
                    $"Reachable block {label} falls through to unreachable instruction {nextInstruction}.");

            labelSuccessors.Add(label, Successor.Unconditional(indexToLabel[nextInstruction]));
        }

        var nodes = labelInstructionCount.OrderBy(pair => LabelToIndex[pair.Key]).Select(pair =>
        {
            var range = new InstructionRange(LabelToIndex[pair.Key], pair.Value);
            var node = createNode(pair.Key, range, labelSuccessors[pair.Key]);
            return KeyValuePair.Create(pair.Key,
                new ControlFlowGraph<TNode>.NodeDefinition(getSuccessor(node), node));
        }).ToDictionary();

        return new ControlFlowGraph<TNode>(Entry, nodes, prettyPrint);
    }

    public readonly record struct InstructionRange(int Start, int Count)
    {
    }

    private void ValidateInstructionIndex(int index, string parameterName)
    {
        if (index < 0 || index >= TotalInstructionCount)
            throw new ArgumentOutOfRangeException(parameterName, index,
                $"Instruction index must be in [0, {TotalInstructionCount}).");
    }
}