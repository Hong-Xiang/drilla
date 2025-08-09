using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using Label = DualDrill.CLSL.Language.Symbol.Label;

namespace DualDrill.CLSL.Frontend;

public sealed class MethodBodyAnalysisModel
{
    public readonly record struct CilInstructionBlock(
        Label Label,
        int InstructionIndex,
        int InstructionCount,
        int ByteOffset,
        int ByteLength
    )
    {
    }

    public ImmutableArray<ParameterInfo> Parameters { get; }
    public ImmutableArray<LocalVariableInfo> LocalVariables { get; }
    public ImmutableArray<int> Offsets { get; }
    private ImmutableArray<Instruction> Instructions { get; }
    public ImmutableArray<Label> Labels { get; }

    private readonly FrozenDictionary<Label, int> LabelIndices;
    private readonly FrozenDictionary<Label, int> LabelCounts;
    private readonly FrozenDictionary<int, Label> OffsetLabels;
    public int LabelToInstructionIndex(Label label) => LabelIndices[label];
    public int LabelToInstructionCount(Label label) => LabelCounts[label];
    public Label? OffsetToLabel(int offset) => OffsetLabels.TryGetValue(offset, out var label) ? label : null;
    public MethodBase Method { get; }
    public MethodBody? Body { get; }
    public bool IsStatic => Method.IsStatic;

    public int InstructionCount => Instructions.Length;
    public int CodeByteSize => Offsets[InstructionCount];

    public FrozenDictionary<int, int> OffsetsToIndex { get; }

    public ControlFlowGraph<CilInstructionBlock> ControlFlowGraph { get; }

    public MethodBodyAnalysisModel(MethodBase method)
    {
        Method = method;
        Body = method.GetMethodBody();
        Parameters = [.. method.GetParameters()];
        Instructions = [.. (method.GetInstructions() ?? [])];

        {
            var localVariables = (method.GetMethodBody()?.LocalVariables ?? []).ToArray();
            var localVariablesFromInsturctions = Instructions.Select(inst => inst.Operand).OfType<LocalVariableInfo>()
                                                             .Distinct().OrderBy(v => v.LocalIndex);
            foreach (var l in localVariablesFromInsturctions)
            {
                localVariables[l.LocalIndex] = l;
            }

            LocalVariables = [.. localVariables];
        }
        {
            var offsets = Instructions.Select(inst => inst.Offset).ToList();
            offsets.Add(method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0);
            Offsets = [.. offsets];
        }

        OffsetsToIndex = Offsets.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

        ControlFlowGraph = GetControlFlowGraph();

        LabelIndices = ControlFlowGraph.Labels().ToFrozenDictionary(l => l, l => ControlFlowGraph[l].InstructionIndex);
        LabelCounts = ControlFlowGraph.Labels().ToFrozenDictionary(l => l, l => ControlFlowGraph[l].InstructionCount);
        OffsetLabels = LabelIndices.ToFrozenDictionary(x => Offsets[x.Value], x => x.Key);
        Labels = [..LabelIndices.OrderBy(x => x.Value).Select(x => x.Key)];
    }

    public IEnumerable<MethodBase> CalledMethods()
    {
        return Instructions.Select(op => op.Operand)
                           .OfType<MethodBase>();
    }

    private ControlFlowGraph<CilInstructionBlock> GetControlFlowGraph()
    {
        var builder = new ControlFlowGraphBuilder(InstructionCount, index => Label.Create(Offsets[index]));

        foreach (var (index, inst) in Instructions.Index())
        {
            int GetTargetIndex()
            {
                var nextOffset = Offsets[index + 1];
                var jumpOffset = inst.Operand switch
                {
                    sbyte v => v,
                    int v => v,
                    _ => 0
                };
                var target = nextOffset + jumpOffset;
                return OffsetsToIndex[target];
            }


            switch (inst.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    builder.AddBr(index, GetTargetIndex());
                    break;
                case FlowControl.Cond_Branch when inst.OpCode.ToILOpCode() == ILOpCode.Switch:
                    throw new NotImplementedException();
                case FlowControl.Cond_Branch:
                    builder.AddBrIf(index, GetTargetIndex());
                    break;
                case FlowControl.Return:
                    builder.AddReturn(index);
                    break;
                case FlowControl.Next:
                case FlowControl.Call:
                    continue;
                default:
                    throw new NotImplementedException($"Controlflow {inst.OpCode.FlowControl} not implemented");
            }
        }

        return builder.Build((label, range) =>
        {
            var count = range.Count;
            return new CilInstructionBlock(
                label,
                range.Start,
                range.Count,
                Offsets[range.Start],
                Offsets[range.Start + range.Count] - Offsets[range.Start]
            );
        });

        // var isLead = new bool[Instructions.Length];
        //
        // foreach (var (index, inst) in Instructions.Index())
        // {

        // }
        //
        // foreach (var (idx, head) in isLead.Index())
        // {
        //     if (!head)
        //     {
        //         continue;
        //     }
        //
        //     var offset = Offsets[idx];
        //     yield return (Label.Create(offset), idx);
        // }
    }

    ParameterInfo GetArg(int index)
    {
        return Parameters[IsStatic ? index : index - 1];
    }

    LocalVariableInfo GetLoc(int index)
    {
        return LocalVariables[index];
    }

    public CilInstructionInfo this[int index] => new(index, Offsets[index], Offsets[index + 1], Instructions[index]);
}