using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using Lokad.ILPack.IL;
using Label = DualDrill.CLSL.Language.Symbol.Label;

namespace DualDrill.CLSL.Frontend;

public sealed class MethodBodyAnalysisModel
{
    private readonly FrozenDictionary<Label, int> LabelCounts;

    private readonly FrozenDictionary<Label, int> LabelIndices;
    private readonly FrozenDictionary<int, Label> OffsetLabels;

    public MethodBodyAnalysisModel(MethodBase method)
    {
        Method = method;
        Body = method.GetMethodBody();
        if (Body?.ExceptionHandlingClauses.Count > 0)
            throw new NotSupportedException($"Exception handling is not supported for method {method}.");

        Parameters = [.. method.GetParameters()];
        var decodedInstructions = (method.GetInstructions() ?? []).ToImmutableArray();

        {
            var localVariables = (method.GetMethodBody()?.LocalVariables ?? []).ToArray();
            var localVariablesFromInsturctions = decodedInstructions.Select(inst => inst.Operand)
                                                                    .OfType<LocalVariableInfo>()
                                                                    .Distinct()
                                                                    .OrderBy(v => v.LocalIndex);
            foreach (var l in localVariablesFromInsturctions) localVariables[l.LocalIndex] = l;

            LocalVariables = [.. localVariables];
        }
        {
            var offsets = decodedInstructions.Select(inst => inst.Offset).ToList();
            offsets.Add(method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0);
            Offsets = [.. offsets];
        }

        OffsetsToIndex = Offsets.Index().ToFrozenDictionary(x => x.Item, x => x.Index);
        Instructions =
        [
            .. decodedInstructions.Select((instruction, index) =>
                new CilInstructionInfo(index, Offsets[index], Offsets[index + 1], instruction))
        ];

        ControlFlowGraph = GetControlFlowGraph();

        LabelIndices = ControlFlowGraph.Labels().ToFrozenDictionary(l => l, l => ControlFlowGraph[l].InstructionIndex);
        LabelCounts = ControlFlowGraph.Labels().ToFrozenDictionary(l => l, l => ControlFlowGraph[l].InstructionCount);
        OffsetLabels = LabelIndices.ToFrozenDictionary(x => Offsets[x.Value], x => x.Key);
        Labels = [.. LabelIndices.OrderBy(x => x.Value).Select(x => x.Key)];
    }

    public ImmutableArray<ParameterInfo> Parameters { get; }
    public ImmutableArray<LocalVariableInfo> LocalVariables { get; }
    public ImmutableArray<int> Offsets { get; }
    public ImmutableArray<CilInstructionInfo> Instructions { get; }
    public ImmutableArray<Label> Labels { get; }
    public MethodBase Method { get; }
    public MethodBody? Body { get; }
    public bool IsStatic => Method.IsStatic;

    public int InstructionCount => Instructions.Length;
    public int CodeByteSize => Offsets[InstructionCount];

    public FrozenDictionary<int, int> OffsetsToIndex { get; }

    public ControlFlowGraph<CilInstructionBlock> ControlFlowGraph { get; }
    public CilInstructionInfo this[int index] => Instructions[index];
    public int LabelToInstructionIndex(Label label) => LabelIndices[label];
    public int LabelToInstructionCount(Label label) => LabelCounts[label];
    public Label? OffsetToLabel(int offset) => OffsetLabels.TryGetValue(offset, out var label) ? label : null;

    public IEnumerable<MethodBase> CalledMethods()
    {
        return Instructions.Select(info => info.Instruction.Operand)
                           .OfType<MethodBase>();
    }

    private ControlFlowGraph<CilInstructionBlock> GetControlFlowGraph()
    {
        var builder = new ControlFlowGraphBuilder(InstructionCount, index => Label.Create(Offsets[index]));

        foreach (var inst in Instructions)
        {
            var opCode = inst.Instruction.OpCode.ToILOpCode();

            int GetTargetIndex()
            {
                var jumpOffset = inst.Instruction.Operand switch
                {
                    sbyte v => v,
                    int v => v,
                    _ => throw new InvalidProgramException(
                        $"Unsupported branch operand at IL_{inst.ByteOffset:X4}.")
                };

                int target;
                try
                {
                    target = checked(inst.NextByteOffset + jumpOffset);
                }
                catch (OverflowException e)
                {
                    throw new InvalidProgramException(
                        $"Branch target overflows at IL_{inst.ByteOffset:X4}.", e);
                }

                if (!OffsetsToIndex.TryGetValue(target, out var targetIndex) || targetIndex >= InstructionCount)
                    throw new InvalidProgramException(
                        $"Branch target IL_{target:X4} from IL_{inst.ByteOffset:X4} is not an instruction boundary.");

                return targetIndex;
            }


            switch (inst.Instruction.OpCode.FlowControl)
            {
                case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                    builder.AddBr(inst.Index, GetTargetIndex());
                    break;
                case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                    builder.AddBrIf(inst.Index, GetTargetIndex());
                    break;
                case FlowControl.Return when CilControlFlow.IsReturn(opCode):
                    builder.AddReturn(inst.Index);
                    break;
                case FlowControl.Next:
                case FlowControl.Call:
                    continue;
                default:
                    throw new NotSupportedException(
                        $"CIL control {opCode} at IL_{inst.ByteOffset:X4} is not supported for method {Method}.");
            }
        }

        return builder.Build(
            (label, range, successor) =>
            {
                var instructions = Instructions.Slice(range.Start, range.Count);
                var last = instructions[^1];
                CilControlFlow terminator = (last.Instruction.OpCode.FlowControl, successor) switch
                {
                    (FlowControl.Branch, UnconditionalSuccessor { Target: var target }) =>
                        new CilControlFlow.Branch(last, target),
                    (FlowControl.Cond_Branch,
                        ConditionalSuccessor { TrueTarget: var branchTarget, FalseTarget: var fallThroughTarget }) =>
                        new CilControlFlow.ConditionalBranch(last, branchTarget, fallThroughTarget),
                    (FlowControl.Return, TerminateSuccessor) =>
                        new CilControlFlow.Return(last),
                    (FlowControl.Next or FlowControl.Call, UnconditionalSuccessor { Target: var target }) =>
                        new CilControlFlow.FallThrough(target),
                    (FlowControl.Next or FlowControl.Call, TerminateSuccessor) =>
                        new CilControlFlow.EndOfCode(),
                    _ => throw new InvalidProgramException(
                        $"CIL control and CFG topology disagree at IL_{last.ByteOffset:X4}.")
                };

                return new CilInstructionBlock(label, instructions, terminator);
            },
            static block => block.Terminator.ToSuccessor());
    }

    public sealed record CilInstructionBlock
    {
        internal CilInstructionBlock(
            Label label,
            ImmutableArray<CilInstructionInfo> instructions,
            CilControlFlow terminator)
        {
            if (instructions.IsDefaultOrEmpty)
                throw new ArgumentException("A CIL basic block must contain at least one instruction.",
                    nameof(instructions));

            var last = instructions[^1];
            var nativeInstruction = terminator switch
            {
                CilControlFlow.Return control => control.Instruction,
                CilControlFlow.Branch control => control.Instruction,
                CilControlFlow.ConditionalBranch control => control.Instruction,
                _ => (CilInstructionInfo?)null
            };
            if (nativeInstruction is { } source &&
                (!source.Equals(last) || !ReferenceEquals(source.Instruction, last.Instruction)))
                throw new ArgumentException(
                    "The native CIL terminator must retain the block's final original instruction.",
                    nameof(terminator));

            Label = label;
            Instructions = instructions;
            Terminator = terminator;
        }

        public Label Label { get; }
        public ImmutableArray<CilInstructionInfo> Instructions { get; }
        public CilControlFlow Terminator { get; }
        public int InstructionIndex => Instructions[0].Index;
        public int InstructionCount => Instructions.Length;
        public int ByteOffset => Instructions[0].ByteOffset;
        public int ByteLength => Instructions[^1].NextByteOffset - ByteOffset;
    }
}