using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Frontend;

public sealed class MethodBodyAnalysisModel
{
    private readonly FrozenDictionary<Label, int> labelIndices;

    public MethodBodyAnalysisModel(
        FunctionDeclaration declaration,
        LinearCode<CilInstructionInfo> rawCode,
        LinearCode<Annotated<CilInstructionInfo, PreStack>> preAnnotatedCode,
        Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis> controlFlow)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(rawCode);
        ArgumentNullException.ThrowIfNull(preAnnotatedCode);
        ArgumentNullException.ThrowIfNull(controlFlow);
        if (!ReferenceEquals(rawCode.Environment, preAnnotatedCode.Environment))
            throw new ArgumentException("Raw and Pre-annotated code must share one method environment.",
                nameof(preAnnotatedCode));
        if (!ReferenceEquals(controlFlow.Node, controlFlow.Annotation.ControlFlowGraph))
            throw new ArgumentException("Control-flow analysis must belong to the stored graph.", nameof(controlFlow));
        ValidateControlFlowSource(preAnnotatedCode, controlFlow.Node);

        Declaration = declaration;
        RawCode = rawCode;
        PreAnnotatedCode = preAnnotatedCode;
        ControlFlow = controlFlow;
        labelIndices = controlFlow.Node.Labels()
                                  .ToFrozenDictionary(
                                      label => label,
                                      label => controlFlow.Node[label].InstructionIndex);
        Labels = [.. labelIndices.OrderBy(pair => pair.Value).Select(pair => pair.Key)];
    }

    public FunctionDeclaration Declaration { get; }
    public LinearCode<CilInstructionInfo> RawCode { get; }
    public LinearCode<Annotated<CilInstructionInfo, PreStack>> PreAnnotatedCode { get; }
    public Annotated<ControlFlowGraph<CilInstructionBlock>, ControlFlowAnalysis> ControlFlow { get; }
    public ImmutableArray<Label> Labels { get; }
    public CilMethodEnvironment Environment => RawCode.Environment;
    public int InstructionCount => RawCode.Count;
    public int CodeByteSize => Environment.CodeByteSize;
    public CilInstructionInfo this[int index] => RawCode[index];

    public int LabelToInstructionIndex(Label label) => labelIndices[label];

    public IEnumerable<System.Reflection.MethodBase> CalledMethods() =>
        PreAnnotatedCode.Instructions
                        .Select(instruction => instruction.Node.Instruction.Operand)
                        .OfType<System.Reflection.MethodBase>();

    private static void ValidateControlFlowSource(
        LinearCode<Annotated<CilInstructionInfo, PreStack>> preAnnotatedCode,
        ControlFlowGraph<CilInstructionBlock> graph)
    {
        var preByIndex = preAnnotatedCode.Instructions.ToFrozenDictionary(item => item.Node.Index);
        var graphIndices = new HashSet<int>();
        foreach (var block in graph.Labels().Select(label => graph[label]))
        foreach (var item in block.Instructions)
        {
            if (!graphIndices.Add(item.Node.Index) ||
                !preByIndex.TryGetValue(item.Node.Index, out var source) ||
                !source.Node.Equals(item.Node) ||
                !ReferenceEquals(source.Node.Instruction, item.Node.Instruction) ||
                !ReferenceEquals(source.Annotation, item.Annotation))
                throw new ArgumentException(
                    "The control-flow graph does not belong to the stored Pre-annotated source.",
                    nameof(graph));
        }

        if (graphIndices.Count != preByIndex.Count)
            throw new ArgumentException(
                "The control-flow graph does not partition the complete reachable Pre-annotated source.",
                nameof(graph));
    }
}

public sealed record CilInstructionBlock
{
    internal CilInstructionBlock(
        Label label,
        ImmutableArray<Annotated<CilInstructionInfo, PreStack>> instructions,
        CilControlFlow terminator)
    {
        if (instructions.IsDefaultOrEmpty)
            throw new ArgumentException("A CIL basic block must contain at least one instruction.",
                nameof(instructions));

        var last = instructions[^1].Node;
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
    public ImmutableArray<Annotated<CilInstructionInfo, PreStack>> Instructions { get; }
    public CilControlFlow Terminator { get; }
    public PreStack EntryStack => Instructions[0].Annotation;
    public int InstructionIndex => Instructions[0].Node.Index;
    public int InstructionCount => Instructions.Length;
    public int ByteOffset => Instructions[0].Node.ByteOffset;
    public int ByteLength => Instructions[^1].Node.NextByteOffset - ByteOffset;
}
