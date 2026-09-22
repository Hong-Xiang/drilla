using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Compiler;

/// <summary>
///     Partitions original instruction positions and binds labels before constructing block payloads.
/// </summary>
public sealed class InstructionBlockPartitioner
{
    private readonly Dictionary<int, ISuccessor> IndexSuccessors = [];

    public InstructionBlockPartitioner(int totalInstructionCount, Func<int, Label> instructionIndexToLabelFactory)
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

    public ImmutableArray<Label> AddSwitch(
        int source,
        IReadOnlyList<int> caseTargets,
        int defaultTarget)
    {
        ValidateInstructionIndex(source, nameof(source));
        ValidateInstructionIndex(defaultTarget, nameof(defaultTarget));
        if (defaultTarget != source + 1)
            throw new ArgumentException("A switch default must be the physical fallthrough instruction.",
                nameof(defaultTarget));

        var targets = caseTargets.Select(target =>
        {
            ValidateInstructionIndex(target, nameof(caseTargets));
            return GetOrCreateLabel(target);
        }).ToImmutableArray();
        var defaultLabel = GetOrCreateLabel(defaultTarget);
        IndexSuccessors.Add(source, Successor.Switch(targets, defaultLabel));
        return targets;
    }

    public void AddReturn(int source)
    {
        ValidateInstructionIndex(source, nameof(source));
        IndexSuccessors.Add(source, new TerminateSuccessor());
        _ = TryGetOrCreateLabel(source + 1, out _);
    }

    public BlockList<TBlock> Build<TBlock>(
        Func<Label, InstructionRange, ISuccessor, TBlock> createBlock,
        Func<TBlock, ISuccessor> getSuccessor,
        Action<BlockList<TBlock>, IndentedTextWriter, PrettyPrintOption> prettyPrint)
        where TBlock : ILabeledEntity
    {
        return BuildReachable(
            Enumerable.Range(0, TotalInstructionCount).ToHashSet(),
            createBlock,
            getSuccessor,
            prettyPrint);
    }

    public BlockList<TBlock> BuildReachable<TBlock>(
        IReadOnlySet<int> reachableInstructionIndices,
        Func<Label, InstructionRange, ISuccessor, TBlock> createBlock,
        Func<TBlock, ISuccessor> getSuccessor,
        Action<BlockList<TBlock>, IndentedTextWriter, PrettyPrintOption> prettyPrint)
        where TBlock : ILabeledEntity
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

        var blocks = labelInstructionCount.OrderBy(pair => LabelToIndex[pair.Key]).Select(pair =>
        {
            var range = new InstructionRange(LabelToIndex[pair.Key], pair.Value);
            var block = createBlock(pair.Key, range, labelSuccessors[pair.Key]);
            if (!ReferenceEquals(pair.Key, block.Label))
                throw new ArgumentException("The block factory must preserve its bound label.", nameof(createBlock));
            return block;
        }).ToImmutableArray();

        return new BlockList<TBlock>(Entry, blocks, getSuccessor, prettyPrint);
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