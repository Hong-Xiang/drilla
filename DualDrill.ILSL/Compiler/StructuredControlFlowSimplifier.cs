using System.Collections.Frozen;
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
        FrozenDictionary<VariableDeclaration, int> VariableStoreCount
    ) : IStructuredControlFlowRegion.IRegionPatternVisitor<IStructuredControlFlowRegion>
{
    private Stack<Label?> LastTarget = [];

    public IStructuredControlFlowRegion VisitBlock(Block block)
    {
        LastTarget.Push(block.Label);
        var seq = ProcessSequence(block.Body);
        LastTarget.Pop();
        return new Block(block.Label, seq);
    }

    public IStructuredControlFlowRegion VisitLoop(Loop loop)
    {
        LastTarget.Push(null);
        var seq = ProcessSequence(loop.Body);
        LastTarget.Pop();
        return new Loop(loop.Label, seq);
    }

    public IStructuredControlFlowRegion VisitIfThenElse(IfThenElse ifThenElse)
    {
        var tb = ProcessSequence(ifThenElse.TrueBody);
        var fb = ProcessSequence(ifThenElse.FalseBody);
        return new IfThenElse(tb, fb);
    }

    StructuredControlFlowElementSequence ProcessSequence(StructuredControlFlowElementSequence sequence)
    {
        var result = new List<IStructuredControlFlowElement>();
        var ip = 0;
        while (ip < sequence.Elements.Length)
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

                    result.Add(ifThenElse.Accept(this));
                    if (!isLast)
                    {
                        LastTarget.Pop();
                    }

                    break;
                }
                case IStructuredControlFlowRegion region:
                {
                    result.Add(region.Accept(this));
                    break;
                }
                case BrInstruction { Target: var target }:
                {
                    if (isLast && target.Equals(LastTarget.Peek()))
                    {
                        ip++;
                        continue;
                    }

                    result.Add(element);
                    break;
                }
                case BrIfInstruction { Target: var target }:
                {
                    if (isLast && target.Equals(LastTarget.Peek()))
                    {
                        ip++;
                        continue;
                    }

                    result.Add(element);
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

public sealed class StructuredControlFlowVariableUsageCounter : IStructuredControlFlowRegion.IRegionPatternVisitor<Unit>
{
    public Dictionary<VariableDeclaration, int> VariableStoreCount { get; } = [];
    public Dictionary<VariableDeclaration, int> VariableLoadCount { get; } = [];

    public Unit VisitBlock(Block block)
    {
        ProcessSequence(block.Body);
        return default;
    }

    public Unit VisitLoop(Loop loop)
    {
        ProcessSequence(loop.Body);
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