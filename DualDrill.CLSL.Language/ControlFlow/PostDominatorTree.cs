using System.Collections.Frozen;
using DualDrill.CLSL.Language.Symbol;
using ExitPostDominanceResult = DualDrill.CLSL.Language.ControlFlow.ExitPostDominance;

namespace DualDrill.CLSL.Language.ControlFlow;

public abstract class ExitPostDominance
{
    private ExitPostDominance()
    {
    }

    public abstract bool MayDiverge { get; }

    public sealed class Block : ExitPostDominance
    {
        public Block(Label target, bool mayDiverge)
        {
            ArgumentNullException.ThrowIfNull(target);
            Target = target;
            MayDiverge = mayDiverge;
        }

        public Label Target { get; }
        public override bool MayDiverge { get; }
    }

    public sealed class FunctionExit(bool mayDiverge) : ExitPostDominance
    {
        public override bool MayDiverge { get; } = mayDiverge;
    }

    public sealed class NoExitPath : ExitPostDominance
    {
        private NoExitPath()
        {
        }

        public static NoExitPath Instance { get; } = new();
        public override bool MayDiverge => true;
    }
}


public sealed class PostDominatorTree
{
    public PostDominatorTree(ControlFlowDFSTree tree)
    {
        ExitPostDominances = Calculate(tree).ToFrozenDictionary();
    }

    private FrozenDictionary<Label, ExitPostDominanceResult> ExitPostDominances { get; }

    public ExitPostDominanceResult ExitPostDominance(Label label) => ExitPostDominances[label];

    private static Dictionary<Label, ExitPostDominanceResult> Calculate(ControlFlowDFSTree tree)
    {
        var labels = tree.Labels;
        var graph = tree.ControlFlowGraph;
        var exitReachable = ExitReachableLabels(labels, graph);
        var mayDiverge = MayDivergeLabels(labels, graph);
        var postDominatorSets = new Dictionary<Label, HashSet<Label>>();
        foreach (var label in exitReachable)
            postDominatorSets[label] = graph.GetSucc(label).Any()
                ? [.. exitReachable]
                : [label];

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var label in exitReachable)
            {
                if (!graph.GetSucc(label).Any()) continue;

                HashSet<Label>? next = null;
                foreach (var successor in graph.GetSucc(label).Where(exitReachable.Contains))
                {
                    if (next is null)
                        next = [.. postDominatorSets[successor]];
                    else
                        next.IntersectWith(postDominatorSets[successor]);
                }

                if (next is null)
                    throw new InvalidOperationException(
                        $"Exit-reachable block {label} has no exit-reachable successor.");

                next.Add(label);
                if (!postDominatorSets[label].SetEquals(next))
                {
                    postDominatorSets[label] = next;
                    changed = true;
                }
            }
        }

        Dictionary<Label, ExitPostDominanceResult> result = [];
        foreach (var label in labels)
        {
            if (!exitReachable.Contains(label))
            {
                result.Add(label, ExitPostDominanceResult.NoExitPath.Instance);
                continue;
            }

            var diverges = mayDiverge.Contains(label);
            var immediate = postDominatorSets[label]
                .Where(candidate => !candidate.Equals(label))
                .OrderByDescending(candidate => postDominatorSets[candidate].Count)
                .FirstOrDefault();
            result.Add(
                label,
                immediate is null
                    ? new ExitPostDominanceResult.FunctionExit(diverges)
                    : new ExitPostDominanceResult.Block(immediate, diverges));
        }

        return result;
    }

    private static HashSet<Label> ExitReachableLabels(
        IEnumerable<Label> labels,
        IControlFlowGraph graph)
    {
        HashSet<Label> result = [];
        Queue<Label> pending = new(labels.Where(label => !graph.GetSucc(label).Any()));
        while (pending.TryDequeue(out var label))
        {
            if (!result.Add(label)) continue;
            foreach (var predecessor in graph.GetPred(label))
                pending.Enqueue(predecessor);
        }

        return result;
    }

    private static HashSet<Label> MayDivergeLabels(
        IEnumerable<Label> labels,
        IControlFlowGraph graph)
    {
        HashSet<Label> result = [.. labels];
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var label in result.ToArray())
                if (!graph.GetSucc(label).Any(result.Contains))
                {
                    result.Remove(label);
                    changed = true;
                }
        }

        return result;
    }
}