using System.Collections.Frozen;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class PostDominatorTree
{
    public PostDominatorTree(ControlFlowDFSTree tree)
    {
        ImmediatePostDominates = Calculate(tree).ToFrozenDictionary();
    }

    private FrozenDictionary<Label, Label?> ImmediatePostDominates { get; }

    public Label? ImmediatePostDominator(Label label) => ImmediatePostDominates[label];

    private static Dictionary<Label, Label?> Calculate(ControlFlowDFSTree tree)
    {
        Dictionary<Label, Label?> result = [];
        var postDominatorSets = new Dictionary<Label, HashSet<Label>>();
        HashSet<Label> terminates = [];
        foreach (var label in tree.Labels) postDominatorSets[label] = [.. tree.Labels];

        foreach (var l in tree.Labels)
            if (!tree.ControlFlowGraph.GetSucc(l).Any())
            {
                terminates.Add(l);
                postDominatorSets[l] = [l];
            }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var label in tree.Labels)
            {
                if (terminates.Contains(label)) continue;
                HashSet<Label> tmp = [.. tree.Labels];
                foreach (var p in tree.ControlFlowGraph.GetSucc(label)) tmp.IntersectWith(postDominatorSets[p]);
                tmp.Add(label);
                var cur = postDominatorSets[label];
                if (cur.Count != tmp.Count)
                {
                    postDominatorSets[label] = tmp;
                    changed = true;
                }
            }
        }

        foreach (var kv in postDominatorSets)
            result.Add(kv.Key, kv.Value
                                 .Where(l => !l.Equals(kv.Key))
                                 .OrderByDescending(l => postDominatorSets[l].Count)
                                 .FirstOrDefault());
        return result;
    }
}