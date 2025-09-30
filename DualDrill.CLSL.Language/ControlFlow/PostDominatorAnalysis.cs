using System.Collections.Immutable;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class PostDominatorAnalysis
{
    private PostDominatorAnalysis(IReadOnlyDictionary<Label, Label?> immediatePostDominators)
    {
        ImmediatePostDominators = immediatePostDominators;
    }

    public IReadOnlyDictionary<Label, Label?> ImmediatePostDominators { get; }

    public Label? GetMergeImmediatePostDominator(Label label) =>
        ImmediatePostDominators.TryGetValue(label, out var merge) ? merge : null;

    public IEnumerable<Label> GetPostDominators(Label label) => throw new NotImplementedException();

    public static PostDominatorAnalysis Create(ControlFlowGraph<Unit> cfg)
    {
        var cfa = cfg.ControlFlowAnalysis();
        ImmutableArray<Label> labels = [.. cfg.Labels()];
        var dominators = labels.ToDictionary(l => l, l => cfa.DominatorTree.Dominators(l).ToImmutableArray());
        var nestedLoop = labels.ToDictionary(l => l, l => dominators[l].Reverse().FirstOrDefault(cfa.IsLoop));
        Dictionary<Label, Label?> immediatePostDominators = [];
        var changed = true;

        void SetPostDominator(Label target, Label? value)
        {
            var nl = nestedLoop[target];
            if (value is null)
            {
                immediatePostDominators[target] = null;
                return;
            }

            // any jump to current scope label should use continue, not break/merge
            if (nl is not null && nl.Equals(value))
            {
                immediatePostDominators[target] = null;
                return;
            }
            //else if (value is not null && (nestedLoop[value]?.Equals(value) ?? false))
            //{
            //    immediatePostDominators[target] = null;
            //}

            immediatePostDominators[target] = value;
            changed = true;
        }

        while (changed && immediatePostDominators.Count < cfg.Count)
        {
            changed = false;
            foreach (var l in labels)
            {
                if (immediatePostDominators.ContainsKey(l)) continue;
                var s = cfg.Successor(l);
                switch (s)
                {
                    case TerminateSuccessor _:
                        SetPostDominator(l, null);
                        break;
                    case UnconditionalSuccessor { Target: Label t }:
                        if (nestedLoop[l]?.Equals(t) ?? false)
                            SetPostDominator(l, null);
                        else
                            SetPostDominator(l, t);
                        break;
                    case ConditionalSuccessor { TrueTarget: Label tt, FalseTarget: Label ft }:
                    {
                        if (!(immediatePostDominators.ContainsKey(tt) &&
                              immediatePostDominators.ContainsKey(ft))) break;
                        if (immediatePostDominators[tt] is null)
                        {
                            SetPostDominator(l, immediatePostDominators[ft]);
                            break;
                        }

                        if (immediatePostDominators[ft] is null)
                        {
                            SetPostDominator(l, immediatePostDominators[tt]);
                            break;
                        }

                        var lc = tt;
                        HashSet<Label> lt = [];
                        while (immediatePostDominators.TryGetValue(lc, out var lci))
                        {
                            if (lci is null) break;
                            lt.Add(lci);
                            lc = lci;
                        }

                        var rc = ft;
                        while (immediatePostDominators.TryGetValue(rc, out var rci) && rci is not null)
                        {
                            if (lt.Contains(rci))
                            {
                                SetPostDominator(l, rci);
                                break;
                            }

                            rc = rci;
                        }
                    }
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        return new PostDominatorAnalysis(immediatePostDominators);
        // how to get inner most loop node -> nearest dominator of loop label
        // immediate post dominator (or merge postdominator?)
        // for loop node and its inner block-nodes (immediate layer of loop, does not include nested loop)
        // the merge node is corresponding to break statement 
        // the jump to loop label is corresponding to continue statement
        // br l -> l == loop label ? null : l
        // br_if t f -> first common lable of dominators of t and f, but how to define first?

        // for each non-loop node
        // br l -> l
        // br_if t f -> first common lable of dominators of t and f, but how to define first?
        throw new NotImplementedException();
    }
}