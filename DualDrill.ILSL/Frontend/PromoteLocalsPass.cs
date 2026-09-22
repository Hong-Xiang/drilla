using System.Collections.Immutable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

/// <summary>
/// Promotes direct, nonescaping i32 and bool function-local loads and stores to block SSA values.
/// Unsupported or incompletely initialized locals remain in storage independently.
/// </summary>
public static class PromoteLocalsPass
{
    public static ControlFlowGraph<CilValueBasicBlock> Run(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<VariableDeclaration> locals,
        bool initLocals)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (locals.IsDefault)
            throw new ArgumentException("Local declarations must be initialized.", nameof(locals));

        var labels = graph.Labels().ToImmutableArray();
        ValidateGraph(graph, labels);
        var infos = CreateLocalInfos(locals);
        AnalyzeUses(graph, labels, infos);

        foreach (var info in infos.Where(info => info.Eligible && info.HasAccess))
            info.Eligible = IsDefinitelyAssigned(graph, labels, info, initLocals);

        var analysis = graph.ControlFlowAnalysis();
        var frontiers = DominanceFrontiers(graph, labels, analysis);
        foreach (var info in infos.Where(info => info.Eligible && info.HasAccess))
        {
            info.LiveIn = LiveIn(graph, labels, info);
            info.PhiBlocks = PhiBlocks(graph, labels, analysis, frontiers, info);
            if (info.PhiBlocks.Contains(graph.EntryLabel))
                info.Eligible = false;
        }

        var promoted = infos.Where(info => info.Eligible && info.HasAccess).ToImmutableArray();
        if (promoted.IsEmpty)
            return graph;

        return Rewrite(graph, labels, analysis, promoted, initLocals);
    }

    private static ImmutableArray<LocalInfo> CreateLocalInfos(ImmutableArray<VariableDeclaration> locals)
    {
        var declarations = new HashSet<VariableDeclaration>(ReferenceEqualityComparer.Instance);
        var result = ImmutableArray.CreateBuilder<LocalInfo>(locals.Length);
        foreach (var local in locals)
        {
            if (local is null)
                throw new ArgumentException("A supplied local declaration is null.", nameof(locals));
            if (!declarations.Add(local))
                throw new ArgumentException("A local declaration was supplied more than once.", nameof(locals));

            result.Add(new LocalInfo(local)
            {
                Eligible = local.AddressSpace.Kind == AddressSpaceKind.Function &&
                           (local.Type.Equals(ShaderType.I32) || local.Type.Equals(ShaderType.Bool))
            });
        }

        return result.ToImmutable();
    }

    private static void ValidateGraph(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels)
    {
        if (labels.Length != graph.Count)
            throw new ArgumentException(
                "The control-flow graph contains definitions disconnected from its entry.",
                nameof(graph));
        var entry = graph[graph.EntryLabel] ??
                    throw new ArgumentException("The control-flow graph entry block is null.", nameof(graph));
        if (!entry.Parameters.IsEmpty)
            throw new ArgumentException("The control-flow graph entry must not have parameters.", nameof(graph));

        foreach (var label in labels)
        {
            var block = graph[label] ??
                        throw new ArgumentException("A control-flow graph block is null.", nameof(graph));
            if (!ReferenceEquals(label, block.Label))
                throw new ArgumentException(
                    "A control-flow graph key does not match its block label.",
                    nameof(graph));
            if (!graph.Successor(label).Equals(block.Successor))
                throw new ArgumentException(
                    "A control-flow graph successor does not match its block terminator.",
                    nameof(graph));

            foreach (var instruction in block.Body.Elements)
                ValidateMemoryInstruction(instruction, graph);
            foreach (var jump in Jumps(block.Body.Last))
                ValidateEdge(graph, label, jump);
        }
    }

    private static void ValidateMemoryInstruction(
        Instruction<IShaderValue, IShaderValue> instruction,
        ControlFlowGraph<CilValueBasicBlock> graph)
    {
        switch (instruction.Operation)
        {
            case LoadOperation:
                if (instruction.OperandCount != 1 ||
                    instruction.Result is null ||
                    instruction.Operand0 is null ||
                    instruction.Operand1 is not null ||
                    !instruction.RestOperands.IsEmpty ||
                    instruction.Operand0.Type is not IPtrType loadPointer ||
                    !instruction.Result.Type.Equals(loadPointer.BaseType))
                    throw new ArgumentException(
                        "A load instruction has invalid arity or types.",
                        nameof(graph));
                break;
            case StoreOperation:
                if (instruction.OperandCount != 2 ||
                    instruction.Result is not null ||
                    instruction.Operand0 is null ||
                    instruction.Operand1 is null ||
                    !instruction.RestOperands.IsEmpty ||
                    instruction.Operand0.Type is not IPtrType storePointer ||
                    !instruction.Operand1.Type.Equals(storePointer.BaseType))
                    throw new ArgumentException(
                        "A store instruction has invalid arity or types.",
                        nameof(graph));
                break;
        }
    }

    private static void ValidateEdge(
        ControlFlowGraph<CilValueBasicBlock> graph,
        Label source,
        RegionJump<IShaderValue> jump)
    {
        var target = graph[jump.Label] ??
                     throw new ArgumentException($"Edge {source} targets a null block.", nameof(graph));
        var parameters = target.Parameters;
        if (jump.Arguments.Length != parameters.Length)
            throw new ArgumentException(
                $"Edge {source} -> {jump.Label} has {jump.Arguments.Length} arguments for " +
                $"{parameters.Length} parameters.",
                nameof(graph));
        for (var index = 0; index < parameters.Length; index++)
        {
            var argument = jump.Arguments[index];
            var parameter = parameters[index];
            if (argument is null || parameter is null || !argument.Type.Equals(parameter.Type))
                throw new ArgumentException(
                    $"Edge {source} -> {jump.Label} argument {index} does not match its parameter type.",
                    nameof(graph));
        }
    }

    private static void AnalyzeUses(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels,
        ImmutableArray<LocalInfo> infos)
    {
        var byPointer = new Dictionary<IShaderValue, LocalInfo>(ReferenceEqualityComparer.Instance);
        foreach (var info in infos)
            byPointer.Add(info.Declaration.Value, info);

        void Escape(IShaderValue? value)
        {
            if (value is not null && byPointer.TryGetValue(value, out var info))
                info.Eligible = false;
        }

        foreach (var label in labels)
        {
            var block = graph[label];
            foreach (var parameter in block.Parameters)
                Escape(parameter);

            foreach (var instruction in block.Body.Elements)
            {
                Escape(instruction.Result);
                switch (instruction.Operation)
                {
                    case LoadOperation:
                        if (byPointer.TryGetValue(instruction.Operand0!, out var loaded))
                            loaded.HasAccess = true;
                        break;
                    case StoreOperation:
                        if (byPointer.TryGetValue(instruction.Operand0!, out var stored))
                            stored.HasAccess = true;
                        Escape(instruction.Operand1);
                        break;
                    default:
                        foreach (var operand in instruction.Operands)
                            Escape(operand);
                        break;
                }
            }

            foreach (var value in TerminatorValues(block.Body.Last))
                Escape(value);
        }
    }

    private static bool IsDefinitelyAssigned(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels,
        LocalInfo info,
        bool initLocals)
    {
        var input = labels.ToDictionary(label => label, _ => true);
        var output = labels.ToDictionary(label => label, _ => true);
        input[graph.EntryLabel] = initLocals;
        output[graph.EntryLabel] = Transfer(graph[graph.EntryLabel], info, initLocals);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var label in labels)
            {
                if (ReferenceEquals(label, graph.EntryLabel))
                    continue;

                var nextInput = graph.Predecessor(label).All(predecessor => output[predecessor]);
                var nextOutput = Transfer(graph[label], info, nextInput);
                if (input[label] == nextInput && output[label] == nextOutput)
                    continue;
                input[label] = nextInput;
                output[label] = nextOutput;
                changed = true;
            }
        }

        foreach (var label in labels)
        {
            var assigned = input[label];
            foreach (var instruction in graph[label].Body.Elements)
            {
                if (IsLoad(instruction, info) && !assigned)
                    return false;
                if (IsStore(instruction, info))
                    assigned = true;
            }
        }

        return true;
    }

    private static bool Transfer(CilValueBasicBlock block, LocalInfo info, bool input) =>
        input || block.Body.Elements.Any(instruction => IsStore(instruction, info));

    private static Dictionary<Label, bool> LiveIn(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels,
        LocalInfo info)
    {
        var use = new Dictionary<Label, bool>();
        var define = new Dictionary<Label, bool>();
        foreach (var label in labels)
        {
            var usedBeforeDefinition = false;
            var defined = false;
            foreach (var instruction in graph[label].Body.Elements)
            {
                if (IsLoad(instruction, info) && !defined)
                    usedBeforeDefinition = true;
                if (IsStore(instruction, info))
                    defined = true;
            }

            use.Add(label, usedBeforeDefinition);
            define.Add(label, defined);
        }

        var input = labels.ToDictionary(label => label, _ => false);
        var output = labels.ToDictionary(label => label, _ => false);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var label in labels.Reverse())
            {
                var nextOutput = graph.GetSucc(label).Any(successor => input[successor]);
                var nextInput = use[label] || (nextOutput && !define[label]);
                if (input[label] == nextInput && output[label] == nextOutput)
                    continue;
                input[label] = nextInput;
                output[label] = nextOutput;
                changed = true;
            }
        }

        return input;
    }

    private static Dictionary<Label, HashSet<Label>> DominanceFrontiers(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels,
        ControlFlowAnalysis analysis)
    {
        var result = labels.ToDictionary(label => label, _ => new HashSet<Label>());
        foreach (var label in labels)
        {
            var predecessors = graph.Predecessor(label);
            var isJoin = ReferenceEquals(label, graph.EntryLabel)
                ? predecessors.Count > 0
                : predecessors.Count > 1;
            if (!isJoin)
                continue;

            var immediateDominator = analysis.DominatorTree.ImmediateDominator(label);
            foreach (var predecessor in predecessors)
            {
                Label? runner = predecessor;
                while (runner is not null && !ReferenceEquals(runner, immediateDominator))
                {
                    result[runner].Add(label);
                    runner = analysis.DominatorTree.ImmediateDominator(runner);
                }
            }
        }

        return result;
    }

    private static HashSet<Label> PhiBlocks(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels,
        ControlFlowAnalysis analysis,
        IReadOnlyDictionary<Label, HashSet<Label>> frontiers,
        LocalInfo info)
    {
        var definitions = labels
            .Where(label => graph[label].Body.Elements.Any(instruction => IsStore(instruction, info)))
            .ToHashSet();
        var result = new HashSet<Label>();
        var work = new Queue<Label>(definitions.OrderBy(analysis.IndexOf));
        while (work.TryDequeue(out var definition))
        {
            foreach (var frontier in frontiers[definition].OrderBy(analysis.IndexOf))
            {
                if (!info.LiveIn[frontier] || !result.Add(frontier))
                    continue;
                if (!definitions.Contains(frontier))
                    work.Enqueue(frontier);
            }
        }

        return result;
    }

    private static ControlFlowGraph<CilValueBasicBlock> Rewrite(
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<Label> labels,
        ControlFlowAnalysis analysis,
        ImmutableArray<LocalInfo> promoted,
        bool initLocals)
    {
        foreach (var info in promoted)
            foreach (var label in info.PhiBlocks)
                info.PhiValues.Add(label, ShaderValue.Intermediate(info.Declaration.Type, info.Declaration.Name));

        var definitions = new Dictionary<Label, CilValueBasicBlock>();
        var substitutions = new Dictionary<IShaderValue, IShaderValue>(ReferenceEqualityComparer.Instance);
        var stacks = new Dictionary<LocalInfo, Stack<IShaderValue>>(ReferenceEqualityComparer.Instance);
        foreach (var info in promoted)
            stacks.Add(info, new Stack<IShaderValue>());
        if (initLocals)
            foreach (var info in promoted)
                stacks[info].Push(InitialValue(info.Declaration.Type));

        void Visit(Label label)
        {
            var block = graph[label];
            var pushed = new Dictionary<LocalInfo, int>(ReferenceEqualityComparer.Instance);
            foreach (var info in promoted)
                pushed.Add(info, 0);
            foreach (var info in promoted)
                if (info.PhiValues.TryGetValue(label, out var phi))
                {
                    stacks[info].Push(phi);
                    pushed[info]++;
                }

            var instructions = ImmutableArray.CreateBuilder<Instruction<IShaderValue, IShaderValue>>();
            foreach (var instruction in block.Body.Elements)
            {
                var info = PointerLocal(instruction, promoted);
                if (instruction.Operation is LoadOperation && info is not null)
                {
                    substitutions.Add(
                        instruction.Result!,
                        Current(stacks[info], info, label));
                    continue;
                }

                if (instruction.Operation is StoreOperation && info is not null)
                {
                    stacks[info].Push(instruction.Operand1!);
                    pushed[info]++;
                    continue;
                }

                instructions.Add(instruction);
            }

            RegionJump<IShaderValue> AppendArguments(RegionJump<IShaderValue> jump)
            {
                var arguments = jump.Arguments.ToBuilder();
                foreach (var info in promoted)
                    if (info.PhiBlocks.Contains(jump.Label))
                        arguments.Add(Current(stacks[info], info, label));
                return new RegionJump<IShaderValue>(jump.Label, arguments.ToImmutable());
            }

            var parameters = block.Parameters.ToBuilder();
            foreach (var info in promoted)
                if (info.PhiValues.TryGetValue(label, out var phi))
                    parameters.Add(phi);
            definitions.Add(label, new CilValueBasicBlock(
                label,
                parameters.ToImmutable(),
                Seq.Create(
                    instructions,
                    block.Body.Last.Select(AppendArguments, static value => value))));

            foreach (var child in analysis.DominatorTree.GetChildren(label))
                Visit(child);

            foreach (var info in promoted)
                for (var index = 0; index < pushed[info]; index++)
                    stacks[info].Pop();
        }

        Visit(graph.EntryLabel);

        IShaderValue Resolve(IShaderValue value)
        {
            var current = value;
            var visited = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
            while (substitutions.TryGetValue(current, out var replacement))
            {
                if (!visited.Add(current))
                    throw new ArgumentException("Promoted load substitutions contain a cycle.", nameof(graph));
                current = replacement;
            }

            return current;
        }

        var result = new Dictionary<Label, ControlFlowGraph<CilValueBasicBlock>.NodeDefinition>();
        foreach (var label in labels)
        {
            var block = definitions[label];
            var rewritten = new CilValueBasicBlock(
                label,
                block.Parameters,
                Seq.Create(
                    block.Body.Elements.Select(instruction =>
                        instruction.Select(Resolve, static value => value)),
                    block.Body.Last.Select(jump => jump.Select(Resolve), Resolve)));
            result.Add(label, new ControlFlowGraph<CilValueBasicBlock>.NodeDefinition(
                rewritten.Successor,
                rewritten));
        }

        return new ControlFlowGraph<CilValueBasicBlock>(graph.EntryLabel, result);
    }

    private static IShaderValue Current(
        Stack<IShaderValue> stack,
        LocalInfo info,
        Label label) =>
        stack.TryPeek(out var value)
            ? value
            : throw new InvalidOperationException(
                $"Promoted local {info.Declaration.Name} has no reaching definition at {label}.");

    private static IShaderValue InitialValue(IShaderType type) =>
        type.Equals(ShaderType.I32)
            ? ShaderValue.Literal(new I32Literal(0))
            : ShaderValue.Literal(new BoolLiteral(false));

    private static LocalInfo? PointerLocal(
        Instruction<IShaderValue, IShaderValue> instruction,
        ImmutableArray<LocalInfo> infos)
    {
        if (instruction.Operation is not (LoadOperation or StoreOperation))
            return null;
        var pointer = instruction.Operand0;
        return infos.FirstOrDefault(info => ReferenceEquals(pointer, info.Declaration.Value));
    }

    private static bool IsLoad(
        Instruction<IShaderValue, IShaderValue> instruction,
        LocalInfo info) =>
        instruction.Operation is LoadOperation &&
        ReferenceEquals(instruction.Operand0, info.Declaration.Value);

    private static bool IsStore(
        Instruction<IShaderValue, IShaderValue> instruction,
        LocalInfo info) =>
        instruction.Operation is StoreOperation &&
        ReferenceEquals(instruction.Operand0, info.Declaration.Value);

    private static IEnumerable<RegionJump<IShaderValue>> Jumps(
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        terminator switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => [branch.Target],
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                [branch.TrueTarget, branch.FalseTarget],
            Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch =>
                [.. branch.CaseTargets, branch.DefaultTarget],
            Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> => [],
            Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
            _ => throw new ArgumentException("The control-flow graph has an unknown terminator.", nameof(terminator))
        };

    private static IEnumerable<IShaderValue> TerminatorValues(
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        terminator switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => branch.Target.Arguments,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                [branch.Condition, .. branch.TrueTarget.Arguments, .. branch.FalseTarget.Arguments],
            Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue> branch =>
                [
                    branch.Selector,
                    .. branch.CaseTargets.SelectMany(target => target.Arguments),
                    .. branch.DefaultTarget.Arguments
                ],
            Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned => [returned.Expr],
            Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
            _ => throw new ArgumentException("The control-flow graph has an unknown terminator.", nameof(terminator))
        };

    private sealed class LocalInfo(VariableDeclaration declaration)
    {
        public VariableDeclaration Declaration { get; } = declaration;
        public bool Eligible { get; set; }
        public bool HasAccess { get; set; }
        public Dictionary<Label, bool> LiveIn { get; set; } = [];
        public HashSet<Label> PhiBlocks { get; set; } = [];
        public Dictionary<Label, IShaderValue> PhiValues { get; } = [];
    }
}
