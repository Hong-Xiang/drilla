using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

public sealed record CilInstructionBlock : ILabeledEntity
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
            CilControlFlow.Switch control => control.Instruction,
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

public sealed class LabelledCilFunctionBody : IFunctionBody, IPrintable
{
    private readonly FrozenDictionary<Label, int> labelIndices;

    internal LabelledCilFunctionBody(
        PreCilFunctionBody pre,
        BlockList<CilInstructionBlock> blocks)
    {
        Pre = pre;
        Blocks = blocks;
        ValidateSource(pre, blocks);
        labelIndices = blocks.Blocks.ToFrozenDictionary(
            block => block.Label,
            block => block.InstructionIndex);
        Labels = [.. labelIndices.OrderBy(pair => pair.Value).Select(pair => pair.Key)];
    }

    public PreCilFunctionBody Pre { get; }
    public RawCilFunctionBody Raw => Pre.Raw;
    public FunctionDeclaration Declaration => Raw.Declaration;
    public LinearCode<CilInstructionInfo> RawCode => Raw.Code;
    public LinearCode<Annotated<CilInstructionInfo, PreStack>> PreAnnotatedCode => Pre.Code;
    public BlockList<CilInstructionBlock> Blocks { get; }
    public ImmutableArray<Label> Labels { get; }
    public ILocalDeclarationContext DeclarationContext => Raw.DeclarationContext;
    public CilMethodEnvironment Environment => RawCode.Environment;
    public int InstructionCount => RawCode.Count;
    public int CodeByteSize => Environment.CodeByteSize;
    public CilInstructionInfo this[int index] => RawCode[index];
    public CilInstructionBlock this[Label label] =>
        Blocks.Blocks.Single(block => ReferenceEquals(block.Label, label));

    public int LabelToInstructionIndex(Label label) => labelIndices[label];

    public void Dump(IndentedTextWriter writer) =>
        Blocks.PrettyPrint(writer, PrettyPrintOption.Default);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        Blocks.PrettyPrint(writer, option);

    private static void ValidateSource(
        PreCilFunctionBody pre,
        BlockList<CilInstructionBlock> blocks)
    {
        var entry = blocks.Blocks.Single(block => ReferenceEquals(block.Label, blocks.EntryLabel));
        if (entry.InstructionIndex != 0)
            throw new ArgumentException("The labelled CIL entry must begin at original instruction index 0.",
                nameof(blocks));

        var preByIndex = pre.Code.Instructions.ToFrozenDictionary(item => item.Node.Index);
        var blocksByLabel = blocks.Blocks.ToDictionary(block => block.Label);
        var reachableLabels = new HashSet<Label>(ReferenceEqualityComparer.Instance);
        void Visit(Label label)
        {
            if (!reachableLabels.Add(label))
                return;
            foreach (var target in blocksByLabel[label].Terminator.ToSuccessor().AllTargets())
                Visit(target);
        }
        Visit(blocks.EntryLabel);
        if (reachableLabels.Count != blocks.Blocks.Length)
            throw new ArgumentException(
                "The labelled CIL blocks contain definitions disconnected from the entry.",
                nameof(blocks));

        var blockIndices = new HashSet<int>();
        foreach (var block in blocks.Blocks)
        {
            foreach (var item in block.Instructions)
                if (!blockIndices.Add(item.Node.Index) ||
                    !preByIndex.TryGetValue(item.Node.Index, out var source) ||
                    !source.Node.Equals(item.Node) ||
                    !ReferenceEquals(source.Node.Instruction, item.Node.Instruction) ||
                    !ReferenceEquals(source.Annotation, item.Annotation))
                    throw new ArgumentException(
                        "The labelled CIL blocks do not belong to the stored Pre-annotated source.",
                        nameof(blocks));
        }

        if (blockIndices.Count != preByIndex.Count)
            throw new ArgumentException(
                "The labelled CIL blocks do not partition the complete reachable Pre-annotated source.",
                nameof(blocks));
    }
}

public sealed class ShaderStackFunctionBody : IFunctionBody, IPrintable
{
    internal ShaderStackFunctionBody(
        LabelledCilFunctionBody source,
        BlockList<ShaderStackBasicBlock> blocks)
    {
        Source = source;
        Blocks = blocks;
        if (!ReferenceEquals(source.Blocks.EntryLabel, blocks.EntryLabel) ||
            source.Blocks.Blocks.Length != blocks.Blocks.Length)
            throw new ArgumentException("Shader-stack blocks must preserve the labelled CIL entry and block count.",
                nameof(blocks));
        foreach (var (cil, shader) in source.Blocks.Blocks.Zip(blocks.Blocks))
            if (!ReferenceEquals(cil.Label, shader.Label) ||
                !cil.EntryStack.Types.Reverse().Select(type => type.ShaderType).SequenceEqual(shader.EntryStack))
                throw new ArgumentException(
                    "Shader-stack blocks must preserve CIL label identity, order and entry-stack types.",
                    nameof(blocks));
        DeclarationContext = new CilStageDeclarationContext(
            source.Raw.DeclarationContext.LocalVariables,
            [.. blocks.Blocks.Select(block => block.Label)],
            []);
    }

    public LabelledCilFunctionBody Source { get; }
    public FunctionDeclaration Declaration => Source.Declaration;
    public BlockList<ShaderStackBasicBlock> Blocks { get; }
    public ILocalDeclarationContext DeclarationContext { get; }

    public void Dump(IndentedTextWriter writer) =>
        Blocks.PrettyPrint(writer, PrettyPrintOption.Default);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        Blocks.PrettyPrint(writer, option);
}

public sealed class ShaderStackControlFlowBody : IFunctionBody, IPrintable
{
    internal ShaderStackControlFlowBody(
        ShaderStackFunctionBody source,
        ControlFlowGraph<ShaderStackBasicBlock> graph)
    {
        Source = source;
        Graph = graph;
        if (!ReferenceEquals(source.Blocks.EntryLabel, graph.EntryLabel) ||
            source.Blocks.Blocks.Length != graph.Count)
            throw new ArgumentException("Shader-stack CFG must preserve every source block.", nameof(graph));
        foreach (var label in graph.Labels())
        {
            var block = graph[label];
            var sourceBlock = source.Blocks.Blocks.Single(candidate => ReferenceEquals(candidate.Label, label));
            if (!ReferenceEquals(block, sourceBlock) ||
                !ReferenceEquals(label, block.Label) ||
                !graph.Successor(label).Equals(block.Successor))
                throw new ArgumentException("A shader-stack CFG definition does not match its block payload.",
                    nameof(graph));
            foreach (var target in graph.Successor(label).AllTargets())
                if (!block.ExitStack.SequenceEqual(graph[target].EntryStack))
                    throw new ArgumentException(
                        $"Shader-stack edge {label} -> {target} has mismatched stack types.",
                        nameof(graph));
        }
    }

    public ShaderStackFunctionBody Source { get; }
    public FunctionDeclaration Declaration => Source.Declaration;
    public ControlFlowGraph<ShaderStackBasicBlock> Graph { get; }
    public ILocalDeclarationContext DeclarationContext => Source.DeclarationContext;

    public void Dump(IndentedTextWriter writer) =>
        CilStagePrettyPrinter.PrintShaderStackControlFlow(this, writer);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        CilStagePrettyPrinter.PrintShaderStackControlFlow(this, writer);
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
        ShaderStackControlFlowBody source,
        ControlFlowGraph<CilValueBasicBlock> graph)
    {
        Source = source;
        Graph = graph;
        foreach (var label in graph.Labels())
        {
            var block = graph[label];
            if (!ReferenceEquals(label, block.Label) || !graph.Successor(label).Equals(block.Successor))
                throw new ArgumentException("A value CFG definition does not match its block payload.", nameof(graph));
            if (block.Body.Last is Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch &&
                !branch.Selector.Type.Equals(ShaderType.I32))
                throw new ArgumentException(
                    $"Value CFG switch selector at {label} must be i32, got {branch.Selector.Type.Name}.",
                    nameof(graph));
        }

        var values = graph.Labels()
                          .SelectMany(label => Values(graph[label]))
                          .Distinct<IShaderValue>(ReferenceEqualityComparer.Instance)
                          .ToImmutableArray();
        DeclarationContext = new CilStageDeclarationContext(
            source.Source.Source.Raw.DeclarationContext.LocalVariables,
            [.. graph.Labels()],
            values);
    }

    public ShaderStackControlFlowBody Source { get; }
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

        public IEnumerable<IShaderValue> Switch(
            IShaderValue selector,
            IReadOnlyList<RegionJump<IShaderValue>> caseTargets,
            RegionJump<IShaderValue> defaultTarget) =>
            [
                selector,
                .. caseTargets.SelectMany(target => target.Arguments),
                .. defaultTarget.Arguments
            ];
    }
}

public sealed class CilValueControlFactsBody : IFunctionBody, IPrintable
{
    internal CilValueControlFactsBody(
        CilValueControlFlowBody source,
        ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>> graph)
    {
        Source = source;
        Graph = graph;
        ValidateSource(source, graph);
    }

    public CilValueControlFlowBody Source { get; }
    public FunctionDeclaration Declaration => Source.Declaration;
    public ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>> Graph { get; }
    public ILocalDeclarationContext DeclarationContext => Source.DeclarationContext;

    public void Dump(IndentedTextWriter writer) =>
        Graph.PrettyPrint(writer, PrettyPrintOption.Default);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        Graph.PrettyPrint(writer, option);

    private static void ValidateSource(
        CilValueControlFlowBody source,
        ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>> graph)
    {
        var sourceLabels = source.Graph.Labels().ToImmutableArray();
        var labels = graph.Labels().ToImmutableArray();
        if (graph.Count != source.Graph.Count ||
            !ReferenceEquals(graph.EntryLabel, source.Graph.EntryLabel) ||
            !labels.SequenceEqual(sourceLabels))
            throw new ArgumentException(
                "The control-facts graph does not preserve the source graph labels and entry.",
                nameof(graph));

        foreach (var (index, label) in labels.Index())
        {
            var annotated = graph[label];
            if (!ReferenceEquals(annotated.Node, source.Graph[label]) ||
                !ReferenceEquals(annotated.Node.Label, label) ||
                !ReferenceEquals(graph.Successor(label), source.Graph.Successor(label)))
                throw new ArgumentException(
                    "The control-facts graph does not preserve source block and edge identity.",
                    nameof(graph));
            if (annotated.Annotation.ReversePostOrderIndex != index)
                throw new ArgumentException(
                    "The control-facts graph does not preserve source reverse-postorder numbering.",
                    nameof(graph));
        }
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
