using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Frontend;

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

public sealed class RawCilFunctionBody : IFunctionBody, IPrintable
{
    internal RawCilFunctionBody(
        FunctionDeclaration declaration,
        LinearCode<CilInstructionInfo> code,
        ImmutableArray<VariableDeclaration> localVariables,
        ISymbolTableView symbols)
    {
        Declaration = declaration;
        Code = code;
        Symbols = symbols;
        DeclarationContext = new CilStageDeclarationContext(localVariables, [], []);
    }

    public FunctionDeclaration Declaration { get; }
    public LinearCode<CilInstructionInfo> Code { get; }
    public ISymbolTableView Symbols { get; }
    public ILocalDeclarationContext DeclarationContext { get; }

    public void Dump(IndentedTextWriter writer) =>
        Code.PrettyPrint(writer, PrettyPrintOption.Default);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        Code.PrettyPrint(writer, option);
}

public sealed class PreCilFunctionBody : IFunctionBody, IPrintable
{
    internal PreCilFunctionBody(
        RawCilFunctionBody raw,
        LinearCode<Annotated<CilInstructionInfo, PreStack>> code)
    {
        Raw = raw;
        Code = code;
    }

    public RawCilFunctionBody Raw { get; }
    public FunctionDeclaration Declaration => Raw.Declaration;
    public LinearCode<Annotated<CilInstructionInfo, PreStack>> Code { get; }
    public ILocalDeclarationContext DeclarationContext => Raw.DeclarationContext;

    public void Dump(IndentedTextWriter writer) =>
        Code.PrettyPrint(writer, PrettyPrintOption.Default);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        Code.PrettyPrint(writer, option);
}

public sealed class MethodBodyAnalysisModel : IFunctionBody, IPrintable
{
    private readonly FrozenDictionary<Label, int> labelIndices;

    public MethodBodyAnalysisModel(
        PreCilFunctionBody pre,
        ControlFlowGraph<CilInstructionBlock> graph)
    {
        Pre = pre;
        ControlFlow = graph;
        ValidateSource(pre, graph);
        labelIndices = graph.Labels()
                            .ToFrozenDictionary(
                                label => label,
                                label => graph[label].InstructionIndex);
        Labels = [.. labelIndices.OrderBy(pair => pair.Value).Select(pair => pair.Key)];
    }

    public PreCilFunctionBody Pre { get; }
    public RawCilFunctionBody Raw => Pre.Raw;
    public FunctionDeclaration Declaration => Raw.Declaration;
    public LinearCode<CilInstructionInfo> RawCode => Raw.Code;
    public LinearCode<Annotated<CilInstructionInfo, PreStack>> PreAnnotatedCode => Pre.Code;
    public ControlFlowGraph<CilInstructionBlock> ControlFlow { get; }
    public ImmutableArray<Label> Labels { get; }
    public ILocalDeclarationContext DeclarationContext => Raw.DeclarationContext;
    public CilMethodEnvironment Environment => RawCode.Environment;
    public int InstructionCount => RawCode.Count;
    public int CodeByteSize => Environment.CodeByteSize;
    public CilInstructionInfo this[int index] => RawCode[index];

    public int LabelToInstructionIndex(Label label) => labelIndices[label];

    public void Dump(IndentedTextWriter writer) =>
        ControlFlow.PrettyPrint(writer, PrettyPrintOption.Default);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        ControlFlow.PrettyPrint(writer, option);

    private static void ValidateSource(
        PreCilFunctionBody pre,
        ControlFlowGraph<CilInstructionBlock> graph)
    {
        if (graph[graph.EntryLabel].InstructionIndex != 0)
            throw new ArgumentException("The control-flow graph entry must begin at original instruction index 0.",
                nameof(graph));

        var preByIndex = pre.Code.Instructions.ToFrozenDictionary(item => item.Node.Index);
        var labels = graph.Labels().ToImmutableArray();
        if (graph.Count != labels.Length)
            throw new ArgumentException(
                "The control-flow graph contains definitions disconnected from its entry.",
                nameof(graph));
        var graphIndices = new HashSet<int>();
        foreach (var label in labels)
        {
            var block = graph[label];
            if (!ReferenceEquals(label, block.Label))
                throw new ArgumentException("A control-flow graph key does not match its block label.", nameof(graph));
            if (!graph.Successor(label).Equals(block.Terminator.ToSuccessor()))
                throw new ArgumentException(
                    "A control-flow graph successor does not match its block terminator.",
                    nameof(graph));

            foreach (var item in block.Instructions)
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

public sealed record class CilValueBasicBlock(
    Label Label,
    ImmutableArray<IShaderValue> Parameters,
    Seq<Instruction<IShaderValue, IShaderValue>,
        ITerminator<RegionJump<IShaderValue>, IShaderValue>> Body)
{
    public ISuccessor Successor => Body.Last.ToSuccessor();
}

public sealed class CilValueControlFlowBody : IFunctionBody, IPrintable
{
    internal CilValueControlFlowBody(
        MethodBodyAnalysisModel source,
        ControlFlowGraph<CilValueBasicBlock> graph)
    {
        Source = source;
        Graph = graph;
        foreach (var label in graph.Labels())
        {
            var block = graph[label];
            if (!ReferenceEquals(label, block.Label) || !graph.Successor(label).Equals(block.Successor))
                throw new ArgumentException("A value CFG definition does not match its block payload.", nameof(graph));
        }

        var values = graph.Labels()
                          .SelectMany(label => Values(graph[label]))
                          .Distinct<IShaderValue>(ReferenceEqualityComparer.Instance)
                          .ToImmutableArray();
        DeclarationContext = new CilStageDeclarationContext(
            source.Raw.DeclarationContext.LocalVariables,
            [.. graph.Labels()],
            values);
    }

    public MethodBodyAnalysisModel Source { get; }
    public FunctionDeclaration Declaration => Source.Declaration;
    public ControlFlowGraph<CilValueBasicBlock> Graph { get; }
    public ILocalDeclarationContext DeclarationContext { get; }

    public void Dump(IndentedTextWriter writer) =>
        CilStagePrettyPrinter.PrintValueControlFlow(this, writer);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        CilStagePrettyPrinter.PrintValueControlFlow(this, writer);

    private static IEnumerable<IShaderValue> Values(CilValueBasicBlock block)
    {
        foreach (var value in block.Parameters)
            yield return value;
        foreach (var instruction in block.Body.Elements)
        {
            if (instruction.Result is { } result)
                yield return result;
            foreach (var operand in instruction.Operands)
                yield return operand;
        }

        foreach (var value in block.Body.Last.Evaluate(ValueTerminatorValues.Instance))
            yield return value;
    }

    private sealed class ValueTerminatorValues
        : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, IEnumerable<IShaderValue>>
    {
        public static ValueTerminatorValues Instance { get; } = new();

        public IEnumerable<IShaderValue> ReturnVoid() => [];
        public IEnumerable<IShaderValue> ReturnExpr(IShaderValue expr) => [expr];
        public IEnumerable<IShaderValue> Br(RegionJump<IShaderValue> target) => target.Arguments;

        public IEnumerable<IShaderValue> BrIf(
            IShaderValue condition,
            RegionJump<IShaderValue> trueTarget,
            RegionJump<IShaderValue> falseTarget) =>
            [condition, .. trueTarget.Arguments, .. falseTarget.Arguments];
    }
}

internal sealed class CilStageDeclarationContext : ILocalDeclarationContext
{
    private readonly FrozenDictionary<Label, int> labelIndices;
    private readonly FrozenDictionary<IShaderValue, int> valueIndices;

    public CilStageDeclarationContext(
        ImmutableArray<VariableDeclaration> localVariables,
        ImmutableArray<Label> labels,
        ImmutableArray<IShaderValue> values)
    {
        LocalVariables = localVariables;
        Labels = labels;
        labelIndices = labels.Index().ToFrozenDictionary(item => item.Item, item => item.Index);
        var indexes = new Dictionary<IShaderValue, int>(ReferenceEqualityComparer.Instance);
        foreach (var (index, value) in values.Index())
            indexes.Add(value, index);
        valueIndices = indexes.ToFrozenDictionary(ReferenceEqualityComparer.Instance);
    }

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
    public int LabelIndex(Label label) => labelIndices[label];
    public int ValueIndex(IShaderValue value) => valueIndices[value];
}
