using System.Collections.Frozen;
using System.Collections.Immutable;
using DotNext.Collections.Generic;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common;

namespace DualDrill.CLSL.Compiler;

public class
    StructuredControlFlowSimplifier(
        FrozenDictionary<VariableDeclaration, int> VariableLoadCount,
        FrozenDictionary<VariableDeclaration, int> VariableStoreCount,
        FrozenSet<Label> DirectJumpTargets,
        FrozenSet<Label> NestedJumpTargets)
    : IStructuredControlFlowRegion.IRegionPatternVisitor<IEnumerable<IStructuredControlFlowElement>>
{
    private FrozenSet<Label> JumpTargets = [..DirectJumpTargets, ..NestedJumpTargets];

    private Stack<Label?> LastTarget = [];

    private HashSet<Label> UsedLabels = [];

    public IEnumerable<IStructuredControlFlowElement> VisitBlock(Block block)
    {
        LastTarget.Push(block.Label);
        var seq = ProcessSequence(block.Body);
        LastTarget.Pop();
        if (UsedLabels.Contains(block.Label))
        {
            UsedLabels.Remove(block.Label);
            if (seq.Elements[^1] is BrInstruction { Target: var t }
                && t.Equals(block.Label))
            {
                return [new Block(block.Label, new([..seq.Elements.Take(seq.Elements.Length - 1)]))];
            }
            else
            {
                return [new Block(block.Label, seq)];
            }
        }
        else
        {
            return seq.Elements;
        }
    }

    public IEnumerable<IStructuredControlFlowElement> VisitLoop(Loop loop)
    {
        LastTarget.Push(null);
        var seq = ProcessSequence(loop.Body);
        LastTarget.Pop();
        UsedLabels.Remove(loop.Label);
        return [new Loop(loop.Label, seq)];
    }

    public IEnumerable<IStructuredControlFlowElement> VisitIfThenElse(IfThenElse ifThenElse)
    {
        var tb = ProcessSequence(ifThenElse.TrueBody);
        var fb = ProcessSequence(ifThenElse.FalseBody);
        if (tb.Elements.Length > 0
            && fb.Elements.Length > 0
            && tb.Elements[^1] is BrInstruction { Target: var tt }
            && fb.Elements[^1] is BrInstruction { Target: var ft }
            && tt.Equals(ft))
        {
            tb = new([..tb.Elements.Take(tb.Elements.Length - 1)]);
            fb = new([..fb.Elements.Take(fb.Elements.Length - 1)]);
        }

        return [new IfThenElse(tb, fb)];

        // var headSame = 0;
        // while (headSame < tb.Elements.Length
        //        && headSame < fb.Elements.Length
        //        && tb.Elements[headSame].Equals(fb.Elements[headSame]))
        // {
        //     headSame++;
        // }
        //
        // var tailSame = 0;
        // while (tailSame < tb.Elements.Length - headSame
        //        && tailSame < fb.Elements.Length - headSame
        //        && tb.Elements[^(tailSame + 1)].Equals(fb.Elements[^(tailSame + 1)]))
        // {
        //     tailSame++;
        // }
        //
        // var head = tb.Elements[..headSame];
        // var tail = tailSame > 0 ? tb.Elements[^tailSame..] : [];
        //
        // var tbMiddle = tb.Elements[headSame..(tb.Elements.Length - tailSame)];
        // var fbMiddle = fb.Elements[headSame..(fb.Elements.Length - tailSame)];
        //
        // var newTb = new StructuredControlFlowElementSequence(tbMiddle);
        // var newFb = new StructuredControlFlowElementSequence(fbMiddle);
        //
        // var result = new List<IStructuredControlFlowElement>(head);
        // result.Add(new IfThenElse(newTb, newFb));
        // result.AddRange(tail);
        //
        // return new Block(Label.Create(), new StructuredControlFlowElementSequence([..result]));
    }

    StructuredControlFlowElementSequence ProcessSequence(StructuredControlFlowElementSequence sequence)
    {
        var result = new List<IStructuredControlFlowElement>();
        var ip = 0;
        bool jumped = false;
        while (ip < sequence.Elements.Length && !jumped)
        {
            var element = sequence.Elements[ip];
            var isLast = ip == sequence.Elements.Length - 1;
            switch (element)
            {
                case IfThenElse ifThenElse:
                {
                    if (!isLast)
                    {
                        LastTarget.Push(null);
                    }

                    result.AddRange(ifThenElse.Accept(this));

                    if (!isLast)
                    {
                        LastTarget.Pop();
                    }

                    break;
                }
                case IStructuredControlFlowRegion region:
                {
                    result.AddRange(region.Accept(this));
                    break;
                }
                case BrInstruction { Target: var target }:
                {
                    if (isLast && target.Equals(LastTarget.Peek()))
                    {
                        ip++;
                        continue;
                    }

                    UsedLabels.Add(target);

                    result.Add(element);
                    jumped = true;
                    break;
                }
                case BrIfInstruction { TrueTarget: var target }:
                {
                    if (isLast && target.Equals(LastTarget.Peek()))
                    {
                        ip++;
                        continue;
                    }


                    UsedLabels.Add(target);
                    result.Add(element);
                    jumped = true;
                    break;
                }
                case StoreSymbolInstruction<VariableDeclaration> stloc:
                {
                    if (VariableLoadCount.TryGetValue(stloc.Target, out var loadCount))
                    {
                        if (loadCount == 1
                            && ip + 1 < sequence.Elements.Length
                            && sequence.Elements[ip + 1] is LoadSymbolValueInstruction<VariableDeclaration> ldLoc
                            && ldLoc.Target.Equals(stloc.Target))
                        {
                            ip += 2;
                            continue;
                        }
                        else
                        {
                            result.Add(stloc);
                        }
                    }
                    else
                    {
                        result.Add(ShaderInstruction.Pop());
                    }


                    break;
                }
                default:
                    result.Add(element);
                    break;
            }

            ip++;
        }

        return new([..result]);
    }
}

public sealed class
    StructuredControlFlowLocalReferencesUsageCounter : IStructuredControlFlowRegion.IRegionPatternVisitor<Unit>
{
    public Dictionary<VariableDeclaration, int> VariableStoreCount { get; } = [];
    public Dictionary<VariableDeclaration, int> VariableLoadCount { get; } = [];
    public HashSet<Label> NestedJumpLabels { get; } = [];
    public HashSet<Label> DirectJumpLabels { get; } = [];

    public Stack<Label> CurrentTarget = [];

    public Unit VisitBlock(Block block)
    {
        CurrentTarget.Push(block.Label);
        ProcessSequence(block.Body);
        CurrentTarget.Pop();
        return default;
    }

    public Unit VisitLoop(Loop loop)
    {
        CurrentTarget.Push(loop.Label);
        ProcessSequence(loop.Body);
        CurrentTarget.Pop();
        return default;
    }

    public Unit VisitIfThenElse(IfThenElse ifThenElse)
    {
        ProcessSequence(ifThenElse.TrueBody);
        ProcessSequence(ifThenElse.FalseBody);
        return default;
    }

    void ProcessSequence(StructuredControlFlowElementSequence sequence)
    {
        foreach (var e in sequence.Elements)
        {
            if (e is IStructuredControlFlowRegion region)
            {
                region.Accept(this);
            }
            else
            {
                switch (e)
                {
                    case BrInstruction { Target: var target }:
                    {
                        // due to validation, target must exist in current stack
                        if (CurrentTarget.Peek().Equals(target))
                        {
                            DirectJumpLabels.Add(target);
                        }
                        else
                        {
                            NestedJumpLabels.Add(target);
                        }

                        break;
                    }
                    case LoadSymbolValueInstruction<VariableDeclaration> load:
                    {
                        if (VariableLoadCount.TryGetValue(load.Target, out int count))
                        {
                            VariableLoadCount[load.Target] = count + 1;
                        }
                        else
                        {
                            VariableLoadCount.Add(load.Target, 1);
                        }

                        break;
                    }
                    case StoreSymbolInstruction<VariableDeclaration> load:
                    {
                        if (VariableStoreCount.TryGetValue(load.Target, out int count))
                        {
                            VariableStoreCount[load.Target] = count + 1;
                        }
                        else
                        {
                            VariableStoreCount.Add(load.Target, 1);
                        }

                        break;
                    }
                }
            }
        }
    }
}