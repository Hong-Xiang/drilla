using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using Lokad.ILPack.IL;
using Label = DualDrill.CLSL.Language.Symbol.Label;

namespace DualDrill.CLSL.Frontend;

public sealed class MethodBodyAnalysisModel
{
    private FrozenDictionary<Label, int>? labelCounts;
    private FrozenDictionary<Label, int>? labelIndices;
    private FrozenDictionary<int, Label>? offsetLabels;
    private ControlFlowGraph<CilInstructionBlock>? controlFlowGraph;
    private ImmutableDictionary<int, ImmutableStack<CilStackType>>? preStackTypes;

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

    }

    public ImmutableArray<ParameterInfo> Parameters { get; }
    public ImmutableArray<LocalVariableInfo> LocalVariables { get; }
    public ImmutableArray<int> Offsets { get; }
    public ImmutableArray<CilInstructionInfo> Instructions { get; }
    public MethodBase Method { get; }
    public MethodBody? Body { get; }
    public bool IsStatic => Method.IsStatic;

    public int InstructionCount => Instructions.Length;
    public int CodeByteSize => Offsets[InstructionCount];

    public FrozenDictionary<int, int> OffsetsToIndex { get; }

    public ImmutableDictionary<int, ImmutableStack<CilStackType>> PreStackTypes =>
        preStackTypes ?? throw new InvalidOperationException($"CIL Pre stacks have not been analyzed for {Method}.");

    public ControlFlowGraph<CilInstructionBlock> ControlFlowGraph =>
        controlFlowGraph ?? throw new InvalidOperationException($"The CIL CFG has not been built for {Method}.");

    public ImmutableArray<Label> Labels { get; private set; } = [];

    public CilInstructionInfo this[int index] => Instructions[index];
    public int LabelToInstructionIndex(Label label) => Require(labelIndices, nameof(labelIndices))[label];
    public int LabelToInstructionCount(Label label) => Require(labelCounts, nameof(labelCounts))[label];

    public Label? OffsetToLabel(int offset) =>
        Require(offsetLabels, nameof(offsetLabels)).TryGetValue(offset, out var label) ? label : null;

    public IEnumerable<MethodBase> CalledMethods()
    {
        return PreStackTypes.Keys.Select(index => Instructions[index].Instruction.Operand)
                           .OfType<MethodBase>();
    }

    internal void Analyze(
        FunctionDeclaration function,
        ISymbolTableView table,
        Action<MethodBase> declareCallee)
    {
        if (preStackTypes is not null)
            return;

        foreach (var instruction in Instructions)
            ValidateControlBoundary(instruction);

        var analyzedPre = CilPreStackAnalyzer.Analyze(this, function, table, declareCallee);
        var graph = GetControlFlowGraph(analyzedPre.Keys.ToFrozenSet(), analyzedPre);
        var analyzedLabelIndices = graph.Labels()
                                        .ToFrozenDictionary(label => label,
                                            label => graph[label].InstructionIndex);
        var analyzedLabelCounts = graph.Labels()
                                       .ToFrozenDictionary(label => label,
                                           label => graph[label].InstructionCount);

        preStackTypes = analyzedPre;
        controlFlowGraph = graph;
        labelIndices = analyzedLabelIndices;
        labelCounts = analyzedLabelCounts;
        offsetLabels = analyzedLabelIndices.ToFrozenDictionary(pair => Offsets[pair.Value], pair => pair.Key);
        Labels = [.. analyzedLabelIndices.OrderBy(pair => pair.Value).Select(pair => pair.Key)];
    }

    internal IEnumerable<int> SuccessorInstructionIndices(CilInstructionInfo instruction)
    {
        var opCode = instruction.Instruction.OpCode.ToILOpCode();
        switch (instruction.Instruction.OpCode.FlowControl)
        {
            case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                yield return ResolveBranchTarget(instruction);
                yield break;
            case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                yield return ResolveBranchTarget(instruction);
                if (instruction.Index + 1 >= InstructionCount)
                    throw new InvalidProgramException(
                        $"Conditional branch at IL_{instruction.ByteOffset:X4} has no fallthrough instruction in {Method}.");
                yield return instruction.Index + 1;
                yield break;
            case FlowControl.Return when CilControlFlow.IsReturn(opCode):
                yield break;
            case FlowControl.Next:
            case FlowControl.Call:
                if (instruction.Index + 1 >= InstructionCount)
                    throw new InvalidProgramException(
                        $"Method {Method} reaches the end of CIL after IL_{instruction.ByteOffset:X4} without a return.");
                yield return instruction.Index + 1;
                yield break;
            default:
                throw new NotSupportedException(
                    $"CIL control {opCode} at IL_{instruction.ByteOffset:X4} is not supported for method {Method}.");
        }
    }

    private void ValidateControlBoundary(CilInstructionInfo instruction)
    {
        var opCode = instruction.Instruction.OpCode.ToILOpCode();
        switch (instruction.Instruction.OpCode.FlowControl)
        {
            case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                _ = ResolveBranchTarget(instruction);
                return;
            case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                _ = ResolveBranchTarget(instruction);
                if (instruction.Index + 1 >= InstructionCount)
                    throw new InvalidProgramException(
                        $"Conditional branch at IL_{instruction.ByteOffset:X4} has no fallthrough instruction in {Method}.");
                return;
            case FlowControl.Return when CilControlFlow.IsReturn(opCode):
            case FlowControl.Next:
            case FlowControl.Call:
                return;
            default:
                throw new NotSupportedException(
                    $"CIL control {opCode} at IL_{instruction.ByteOffset:X4} is not supported for method {Method}.");
        }
    }

    internal int ResolveBranchTarget(CilInstructionInfo instruction)
    {
        var jumpOffset = instruction.Instruction.Operand switch
        {
            sbyte value => value,
            int value => value,
            _ => throw new InvalidProgramException(
                $"Unsupported branch operand at IL_{instruction.ByteOffset:X4} in {Method}.")
        };

        int target;
        try
        {
            target = checked(instruction.NextByteOffset + jumpOffset);
        }
        catch (OverflowException exception)
        {
            throw new InvalidProgramException(
                $"Branch target overflows at IL_{instruction.ByteOffset:X4} in {Method}.",
                exception);
        }

        if (!OffsetsToIndex.TryGetValue(target, out var targetIndex) || targetIndex >= InstructionCount)
            throw new InvalidProgramException(
                $"Branch target IL_{target:X4} from IL_{instruction.ByteOffset:X4} in {Method} " +
                "is not an instruction boundary.");

        return targetIndex;
    }

    private ControlFlowGraph<CilInstructionBlock> GetControlFlowGraph(
        IReadOnlySet<int> reachable,
        IReadOnlyDictionary<int, ImmutableStack<CilStackType>> analyzedPre)
    {
        var builder = new ControlFlowGraphBuilder(InstructionCount, index => Label.Create(Offsets[index]));

        foreach (var index in reachable.Order())
        {
            var inst = Instructions[index];
            var opCode = inst.Instruction.OpCode.ToILOpCode();

            switch (inst.Instruction.OpCode.FlowControl)
            {
                case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                    builder.AddBr(inst.Index, ResolveBranchTarget(inst));
                    break;
                case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                    builder.AddBrIf(inst.Index, ResolveBranchTarget(inst));
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

        return builder.BuildReachable(
            reachable,
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

                return new CilInstructionBlock(label, instructions, terminator)
                {
                    EntryStackTypes = analyzedPre[instructions[0].Index]
                };
            },
            static block => block.Terminator.ToSuccessor());
    }

    private static T Require<T>(T? value, string property) where T : class =>
        value ?? throw new InvalidOperationException($"{property} is unavailable before CIL analysis.");

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
        public ImmutableStack<CilStackType> EntryStackTypes { get; internal init; } = [];
        public int InstructionIndex => Instructions[0].Index;
        public int InstructionCount => Instructions.Length;
        public int ByteOffset => Instructions[0].ByteOffset;
        public int ByteLength => Instructions[^1].NextByteOffset - ByteOffset;
    }
}