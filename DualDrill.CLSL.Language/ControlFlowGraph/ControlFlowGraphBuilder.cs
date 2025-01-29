using System.Diagnostics.CodeAnalysis;

namespace DualDrill.CLSL.Language.ControlFlowGraph;


/// <summary>
/// ControlFlowGraphBuilder build control flow graph from linear instructions with control flow instructions like
/// br, br.if, switch, return, etc.
/// When building nodes of control flow graph, basis blocks could be constructed
/// </summary>
/// <typeparam name="TNode"></typeparam>
public sealed class ControlFlowGraphBuilder
{
    public delegate Label CreateLabel(int index);
    public readonly record struct InstructionRange(int Start, int Count) { }

    public Label Entry => IndexToLabels[0];
    Dictionary<int, Label> IndexToLabels { get; } = [];
    Dictionary<Label, int> LabelIndex { get; } = [];
    Dictionary<int, ISuccessor> IndexSuccessors = [];
    int InstructionCount { get; }

    CreateLabel LabelFactory { get; }

    /// <summary>
    /// Get label starts with instruction of index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Label this[int index] => IndexToLabels[index];

    public ControlFlowGraphBuilder(int instructionCount, CreateLabel labelFactory)
    {
        if (!(instructionCount >= 1))
        {
            throw new ArgumentException($"instruction count >= 1 is required, got {instructionCount}");
        }
        LabelFactory = labelFactory;
        InstructionCount = instructionCount;
        AddLabel(0, labelFactory(0));
    }

    void AddLabel(int index, Label label)
    {
        IndexToLabels[index] = label;
        LabelIndex[label] = index;
    }

    Label GetOrCreateLabel(int index)
    {
        if (IndexToLabels.TryGetValue(index, out var result))
        {
            return result;
        }
        else
        {
            var label = LabelFactory(index);
            AddLabel(index, label);
            return label;
        }
    }

    bool TryGetOrCreateLabel(int index, [NotNullWhen(true)] out Label? result)
    {
        if (index >= InstructionCount)
        {
            result = default;
            return false;
        }
        else
        {
            result = GetOrCreateLabel(index);
            return true;
        }
    }


    public Label AddBr(int source, int target)
    {
        var targetLabel = GetOrCreateLabel(target);
        IndexSuccessors.Add(source, new BrOrNextSuccessor(targetLabel));
        _ = TryGetOrCreateLabel(source + 1, out var _);
        return targetLabel;
    }
    public Label AddBrIf(int source, int target)
    {
        var trueLabel = GetOrCreateLabel(target);
        var falseLabel = GetOrCreateLabel(source + 1);
        IndexSuccessors.Add(source, new BrIfSuccessor(trueLabel, falseLabel));
        return trueLabel;
    }
    public void AddReturn(int source)
    {
        IndexSuccessors.Add(source, new ReturnOrTerminateSuccessor());
        _ = TryGetOrCreateLabel(source + 1, out var _);
    }
    public ControlFlowGraph<TNode> Build<TNode>(Func<Label, InstructionRange, TNode> createNode)
    {
        Dictionary<Label, int> labelInstructionCount = [];
        var indexToLabel = new Label[InstructionCount];
        Dictionary<Label, ISuccessor> labelSuccessors = [];

        // scan instructions to get following maps
        // label -> instructionCount
        // index -> containing label
        // label -> successor
        Label? current = default;
        for (var i = 0; i < InstructionCount; i++)
        {
            if (IndexToLabels.TryGetValue(i, out var next))
            {
                current = next;
            }
            if (current is null)
            {
                throw new NullReferenceException($"Can not find associated label");
            }
            labelInstructionCount.TryAdd(current, 0);
            labelInstructionCount[current]++;
            indexToLabel[i] = current;
            if (IndexSuccessors.TryGetValue(i, out var s))
            {
                labelSuccessors.Add(current, s);
            }
        }

        // handling normal fall through suceessors
        foreach (var (label, idx) in LabelIndex)
        {
            if (!labelSuccessors.ContainsKey(label))
            {
                var nextInst = idx + labelInstructionCount[label];
                if (nextInst >= InstructionCount - 1)
                {
                    labelSuccessors.Add(label, new ReturnOrTerminateSuccessor());
                }
                else
                {
                    labelSuccessors.Add(label, new BrOrNextSuccessor(indexToLabel[nextInst]));
                }
            }
        }

        var nodes = labelInstructionCount.Select(kv =>
        {
            var range = new InstructionRange(LabelIndex[kv.Key], labelInstructionCount[kv.Key]);
            var node = createNode(kv.Key, range);
            return KeyValuePair.Create(kv.Key, node);
        }).ToDictionary();

        //var predecessors = nodes.Keys.Select(n => KeyValuePair.Create(n, new HashSet<Label>())).ToDictionary();
        //foreach (var (l, s) in successors)
        //{
        //    s.Traverse((t) =>
        //    {
        //        predecessors[t].Add(l);
        //    });
        //}

        //Predecessors = predecessors.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.ToFrozenSet())).ToFrozenDictionary();

        return new ControlFlowGraph<TNode>(
            Entry,
            nodes,
            labelSuccessors
        );
    }
}
