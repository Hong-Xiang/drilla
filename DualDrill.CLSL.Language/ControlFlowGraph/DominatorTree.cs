using System.Collections.Frozen;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed class DominatorTree
{
    public FrozenDictionary<Label, int> ReversePostorderNumberingTable { get; }
    /// <summary>
    /// Get all children of a node in dominator tree (ordered by reverse postorder numbering)
    /// which contains all nodes n which satisfies `ImmediateDominator(n) = node`
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public IEnumerable<Label> GetChildren(Label node)
    {
        throw new NotImplementedException();
    }
    public Label? ImmediateDominator(Label node)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// All dominators of given node, which is actually a path from root to given node in dominator tree
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public IEnumerable<Label> DominatorSet(Label node) => DominatorSets[node];

    FrozenDictionary<Label, IEnumerable<Label>> DominatorSets { get; }
    FrozenDictionary<Label, Label> ImmediateDominators { get; }
    private DominatorTree(
        FrozenDictionary<Label, int> reversePostgorderNumberingTable,
        FrozenDictionary<Label, IEnumerable<Label>> donminatorSets
    )
    {
        ReversePostorderNumberingTable = reversePostgorderNumberingTable;
        DominatorSets = donminatorSets;
    }

    public static DominatorTree Create<TNode>(ControlFlowGraph<TNode> graph)
    {
        throw new NotImplementedException();
    }
}
