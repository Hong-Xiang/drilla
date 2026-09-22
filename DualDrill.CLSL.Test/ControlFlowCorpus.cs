using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.CodeDom.Compiler;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

namespace DualDrill.CLSL.Test;

internal static class ControlFlowCorpus
{
    internal const int StepLimit = 10_000;
    internal const uint SampleSeed = 20_260_920;
    internal const int SampleCount = 64;

    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    internal sealed record GraphSpec(
        string Id,
        ImmutableArray<ImmutableArray<int>> Targets)
    {
        internal int NodeCount => Targets.Length;
        internal int EdgeCount => Targets.Sum(static targets => targets.Length);
        internal string Encoding => string.Join(
            ";",
            Targets.Select(static targets => targets.IsEmpty
                ? "R"
                : string.Join(",", targets)));
    }

    internal readonly record struct Arm(int Source, int Index);

    internal sealed record ReferenceExecution(
        int Result,
        ImmutableArray<int> Trace,
        ImmutableArray<Arm> Arms);

    internal sealed record Witness(
        string Id,
        ImmutableArray<Value> Arguments,
        Arm? RequiredArm);

    internal sealed record RawCase(
        string Id,
        FunctionDeclaration Declaration,
        ControlFlowGraph<CilValueBasicBlock> Graph,
        ImmutableArray<VariableDeclaration> Locals,
        bool InitLocals,
        ILocalDeclarationContext Context,
        ImmutableDictionary<Label, int> Identity,
        string Input);

    internal sealed record CompiledCase(
        RawCase Raw,
        ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>> Facts,
        RegionFunctionBody Region,
        RegionFunctionBody Normalized,
        SlangFunctionBody Target,
        string Source);

    internal static ImmutableArray<GraphSpec> ExhaustiveForward()
    {
        var cases = ImmutableArray.CreateBuilder<GraphSpec>();
        for (var nodeCount = 1; nodeCount <= 4; nodeCount++)
        {
            var current = new ImmutableArray<int>[nodeCount];
            var code = 0;

            void Visit(int node)
            {
                if (node == nodeCount)
                {
                    var targets = current.ToImmutableArray();
                    if (AllReachable(targets))
                        cases.Add(new GraphSpec(
                            $"forward-{nodeCount}-{code.ToString("D4", CultureInfo.InvariantCulture)}",
                            targets));
                    code++;
                    return;
                }

                foreach (var option in ForwardOptions(node, nodeCount))
                {
                    current[node] = option;
                    Visit(node + 1);
                }
            }

            Visit(0);
        }

        return cases.ToImmutable();
    }

    internal static ImmutableArray<GraphSpec> SampledForward()
    {
        var random = new StableRandom(SampleSeed);
        var encodings = new HashSet<string>(StringComparer.Ordinal);
        var cases = ImmutableArray.CreateBuilder<GraphSpec>(SampleCount);
        while (cases.Count < SampleCount)
        {
            var nodeCount = 5 + cases.Count % 8;
            var targets = Enumerable.Range(0, nodeCount)
                .Select(static _ => new List<int>(2))
                .ToArray();
            for (var child = 1; child < nodeCount; child++)
            {
                var parents = Enumerable.Range(0, child)
                    .Where(parent => targets[parent].Count < 2)
                    .ToArray();
                targets[parents[random.Next(parents.Length)]].Add(child);
            }

            for (var source = 0; source < nodeCount - 1; source++)
                if (targets[source].Count < 2 && random.Next(2) == 1)
                    targets[source].Add(source + 1 + random.Next(nodeCount - source - 1));

            var frozen = targets.Select(static target => target.ToImmutableArray()).ToImmutableArray();
            var encoding = string.Join(";", frozen.Select(static target => string.Join(",", target)));
            if (!encodings.Add(encoding))
                continue;
            cases.Add(new GraphSpec(
                $"sample-{cases.Count.ToString("D2", CultureInfo.InvariantCulture)}",
                frozen));
        }

        return cases.ToImmutable();
    }

    internal static RawCase Forward(GraphSpec spec, string labelPrefix = "", bool reverseStorage = false)
    {
        var labels = Enumerable.Range(0, spec.NodeCount)
            .Select(index => Label.Create($"{labelPrefix}n{index}"))
            .ToImmutableArray();
        var parameters = Enumerable.Range(0, spec.NodeCount)
            .Select(index => new ParameterDeclaration($"choose{index}", ShaderType.Bool, []))
            .ToImmutableArray();
        var declaration = new FunctionDeclaration(
            $"Corpus_{spec.Id.Replace('-', '_')}_{labelPrefix}",
            parameters,
            new FunctionReturn(ShaderType.I32, []),
            []);
        var accumulators = Enumerable.Range(0, spec.NodeCount)
            .Select(static _ => ShaderValue.Intermediate(ShaderType.I32))
            .ToImmutableArray();
        var blocks = new CilValueBasicBlock[spec.NodeCount];

        for (var node = 0; node < spec.NodeCount; node++)
        {
            var instructions = ImmutableArray.CreateBuilder<Instruction<IShaderValue, IShaderValue>>();
            IShaderValue value;
            var blockParameters = node == 0
                ? ImmutableArray<IShaderValue>.Empty
                : ImmutableArray.Create<IShaderValue>(accumulators[node]);
            if (node == 0)
            {
                value = Int(1);
            }
            else
            {
                value = ShaderValue.Intermediate(ShaderType.I32);
                instructions.Add(Add(value, accumulators[node], Int(node + 1)));
            }

            ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator;
            var targets = spec.Targets[node];
            if (targets.IsEmpty)
            {
                terminator = Terms.ReturnExpr(value);
            }
            else if (targets.Length == 1)
            {
                terminator = Terms.Br(new(labels[targets[0]], [value]));
            }
            else
            {
                var condition = ShaderValue.Intermediate(ShaderType.Bool);
                instructions.Add(Instruction.Factory.Load(
                    default,
                    new LoadOperation(),
                    condition,
                    parameters[node].Value));
                terminator = Terms.BrIf(
                    condition,
                    new(labels[targets[0]], [value]),
                    new(labels[targets[1]], [value]));
            }

            blocks[node] = Block(labels[node], blockParameters, instructions, terminator);
        }

        var order = reverseStorage
            ? Enumerable.Range(0, spec.NodeCount).Reverse()
            : Enumerable.Range(0, spec.NodeCount);
        var graph = Graph(labels[0], order.Select(index => blocks[index]));
        return CreateRawCase(
            spec.Id + (reverseStorage ? "-reordered" : labelPrefix.Length == 0 ? "" : "-renamed"),
            declaration,
            graph,
            [],
            false,
            $"id={spec.Id}{Environment.NewLine}encoding={spec.Encoding}{Environment.NewLine}" +
            $"nodes={spec.NodeCount} edges={spec.EdgeCount}{Environment.NewLine}",
            labels.Select((label, index) => KeyValuePair.Create(label, index)).ToImmutableDictionary());
    }

    internal static ReferenceExecution Reference(GraphSpec spec, ImmutableArray<Value> arguments)
    {
        var trace = ImmutableArray.CreateBuilder<int>();
        var arms = ImmutableArray.CreateBuilder<Arm>();
        var result = 0;
        var node = 0;
        while (true)
        {
            trace.Add(node);
            result += node + 1;
            var targets = spec.Targets[node];
            if (targets.IsEmpty)
                return new ReferenceExecution(result, trace.ToImmutable(), arms.ToImmutable());
            var arm = targets.Length == 1 || arguments[node].Bool ? 0 : 1;
            arms.Add(new Arm(node, arm));
            node = targets[arm];
        }
    }

    internal static ImmutableArray<Witness> Witnesses(GraphSpec spec)
    {
        var witnesses = ImmutableArray.CreateBuilder<Witness>();
        for (var source = 0; source < spec.NodeCount; source++)
            for (var arm = 0; arm < spec.Targets[source].Length; arm++)
            {
                var required = new Arm(source, arm);
                var choices = PathChoices(spec, source);
                choices[source] = arm;
                var arguments = Enumerable.Range(0, spec.NodeCount)
                    .Select(index => (Value)new Value.Boolean(
                        spec.Targets[index].Length == 2 && choices.GetValueOrDefault(index, 1) == 0))
                    .ToImmutableArray();
                var reference = Reference(spec, arguments);
                if (!reference.Arms.Contains(required))
                    throw new InvalidOperationException($"{spec.Id}: witness does not reach arm {required}.");
                witnesses.Add(new Witness($"arm-{source}-{arm}", arguments, required));
            }

        if (witnesses.Count == 0)
            witnesses.Add(new Witness(
                "return",
                Enumerable.Range(0, spec.NodeCount)
                    .Select(static _ => (Value)new Value.Boolean(false))
                    .ToImmutableArray(),
                null));
        return witnesses.ToImmutable();
    }

    internal static RawCase SameTarget()
    {
        var entry = Label.Create("same-entry");
        var exit = Label.Create("same-exit");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("SameTarget11Or29", [choose]);
        var blocks = new[]
        {
            Block(entry, [], [
                Instruction.Factory.Load(default, new LoadOperation(), condition, choose.Value)
            ], Terms.BrIf(condition, new(exit, [Int(11)]), new(exit, [Int(29)]))),
            Block(exit, [result], [], Terms.ReturnExpr(result))
        };
        return Case("hand-same-target", declaration, [.. blocks], entry);
    }

    internal static RawCase SameTargetSwitch()
    {
        var entry = Label.Create("switch-same-entry");
        var join = Label.Create("switch-same-join");
        var input = new ParameterDeclaration("selector", ShaderType.I32, []);
        var selector = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("SameTargetSwitch", [input]);
        var blocks = new[]
        {
            Block(entry, [], [
                Instruction.Factory.Load(default, new LoadOperation(), selector, input.Value)
            ], Terms.Switch(
                selector,
                [new(join, [Int(11)]), new(join, [Int(29)])],
                new(join, [Int(41)]))),
            Block(join, [result], [], Terms.ReturnExpr(result))
        };
        return Case("hand-switch-same-target", declaration, [.. blocks], entry);
    }

    internal static RawCase LoopDispatchSwitch()
    {
        var entry = Label.Create("switch-loop-entry");
        var header = Label.Create("switch-loop-header");
        var dispatch = Label.Create("switch-loop-dispatch");
        var exit = Label.Create("switch-loop-exit");
        var selectorInput = new ParameterDeclaration("selector", ShaderType.I32, []);
        var countInput = new ParameterDeclaration("count", ShaderType.I32, []);
        var selector = ShaderValue.Intermediate(ShaderType.I32);
        var count = ShaderValue.Intermediate(ShaderType.I32);
        var n = ShaderValue.Intermediate(ShaderType.I32);
        var a = ShaderValue.Intermediate(ShaderType.I32);
        var b = ShaderValue.Intermediate(ShaderType.I32);
        var carriedSelector = ShaderValue.Intermediate(ShaderType.I32);
        var active = ShaderValue.Intermediate(ShaderType.Bool);
        var dispatchN = ShaderValue.Intermediate(ShaderType.I32);
        var dispatchA = ShaderValue.Intermediate(ShaderType.I32);
        var dispatchB = ShaderValue.Intermediate(ShaderType.I32);
        var dispatchSelector = ShaderValue.Intermediate(ShaderType.I32);
        var decremented = ShaderValue.Intermediate(ShaderType.I32);
        var incrementedA = ShaderValue.Intermediate(ShaderType.I32);
        var outA = ShaderValue.Intermediate(ShaderType.I32);
        var outB = ShaderValue.Intermediate(ShaderType.I32);
        var tens = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("LoopDispatchSwitch", [selectorInput, countInput]);
        var blocks = new[]
        {
            Block(entry, [], [
                Instruction.Factory.Load(default, new LoadOperation(), selector, selectorInput.Value),
                Instruction.Factory.Load(default, new LoadOperation(), count, countInput.Value)
            ], Terms.Br(new(header, [count, Int(1), Int(3), selector]))),
            Block(header, [n, a, b, carriedSelector], [
                GreaterThan(active, n, Int(0))
            ], Terms.BrIf(
                active,
                new(dispatch, [n, a, b, carriedSelector]),
                new(exit, [a, b]))),
            Block(dispatch, [dispatchN, dispatchA, dispatchB, dispatchSelector], [
                Subtract(decremented, dispatchN, Int(1)),
                Add(incrementedA, dispatchA, Int(1))
            ], Terms.Switch(
                dispatchSelector,
                [
                    new(header, [decremented, dispatchB, dispatchA, dispatchSelector]),
                    new(header, [decremented, incrementedA, dispatchB, dispatchSelector]),
                    new(exit, [dispatchA, dispatchB])
                ],
                new(exit, [dispatchB, dispatchA]))),
            Block(exit, [outA, outB], [
                Multiply(tens, outA, Int(10)),
                Add(result, tens, outB)
            ], Terms.ReturnExpr(result))
        };
        return Case("hand-switch-loop-dispatch", declaration, [.. blocks], entry);
    }

    internal static RawCase ParallelSwap()
    {
        var entry = Label.Create("swap-entry");
        var loop = Label.Create("swap-loop");
        var exit = Label.Create("swap-exit");
        var count = new ParameterDeclaration("count", ShaderType.I32, []);
        var loadedCount = ShaderValue.Intermediate(ShaderType.I32);
        var enter = ShaderValue.Intermediate(ShaderType.Bool);
        var a = ShaderValue.Intermediate(ShaderType.I32);
        var b = ShaderValue.Intermediate(ShaderType.I32);
        var remaining = ShaderValue.Intermediate(ShaderType.I32);
        var decremented = ShaderValue.Intermediate(ShaderType.I32);
        var again = ShaderValue.Intermediate(ShaderType.Bool);
        var finalA = ShaderValue.Intermediate(ShaderType.I32);
        var finalB = ShaderValue.Intermediate(ShaderType.I32);
        var tens = ShaderValue.Intermediate(ShaderType.I32);
        var answer = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("ParallelSwap", [count]);
        var blocks = new[]
        {
            Block(entry, [], [
                Instruction.Factory.Load(default, new LoadOperation(), loadedCount, count.Value),
                GreaterThan(enter, loadedCount, Int(0))
            ], Terms.BrIf(
                enter,
                new(loop, [Int(1), Int(3), loadedCount]),
                new(exit, [Int(1), Int(3)]))),
            Block(loop, [a, b, remaining], [
                Subtract(decremented, remaining, Int(1)),
                GreaterThan(again, decremented, Int(0))
            ], Terms.BrIf(
                again,
                new(loop, [b, a, decremented]),
                new(exit, [b, a]))),
            Block(exit, [finalA, finalB], [
                Multiply(tens, finalA, Int(10)),
                Add(answer, tens, finalB)
            ], Terms.ReturnExpr(answer))
        };
        return Case("hand-parallel-swap", declaration, [.. blocks], entry);
    }

    internal static RawCase MultipleLatchesAndExits()
    {
        var entry = Label.Create("multi-entry");
        var header = Label.Create("multi-header");
        var work = Label.Create("multi-work");
        var left = Label.Create("multi-left-latch");
        var right = Label.Create("multi-right-latch");
        var exitChoice = Label.Create("multi-exit-choice");
        var firstExit = Label.Create("multi-first-exit");
        var secondExit = Label.Create("multi-second-exit");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var count = new ParameterDeclaration("count", ShaderType.I32, []);
        var loadedChoose = ShaderValue.Intermediate(ShaderType.Bool);
        var loadedCount = ShaderValue.Intermediate(ShaderType.I32);
        var state = ShaderValue.Intermediate(ShaderType.I32);
        var remaining = ShaderValue.Intermediate(ShaderType.I32);
        var selector = ShaderValue.Intermediate(ShaderType.Bool);
        var active = ShaderValue.Intermediate(ShaderType.Bool);
        var workState = ShaderValue.Intermediate(ShaderType.I32);
        var workRemaining = ShaderValue.Intermediate(ShaderType.I32);
        var workSelector = ShaderValue.Intermediate(ShaderType.Bool);
        var exitState = ShaderValue.Intermediate(ShaderType.I32);
        var leftState = ShaderValue.Intermediate(ShaderType.I32);
        var leftRemaining = ShaderValue.Intermediate(ShaderType.I32);
        var leftSelector = ShaderValue.Intermediate(ShaderType.Bool);
        var rightState = ShaderValue.Intermediate(ShaderType.I32);
        var rightRemaining = ShaderValue.Intermediate(ShaderType.I32);
        var rightSelector = ShaderValue.Intermediate(ShaderType.Bool);
        var leftNextState = ShaderValue.Intermediate(ShaderType.I32);
        var leftNextRemaining = ShaderValue.Intermediate(ShaderType.I32);
        var leftAgain = ShaderValue.Intermediate(ShaderType.Bool);
        var rightNextState = ShaderValue.Intermediate(ShaderType.I32);
        var rightNextRemaining = ShaderValue.Intermediate(ShaderType.I32);
        var firstState = ShaderValue.Intermediate(ShaderType.I32);
        var secondState = ShaderValue.Intermediate(ShaderType.I32);
        var firstAnswer = ShaderValue.Intermediate(ShaderType.I32);
        var secondAnswer = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("MultipleLatchesAndExits", [choose, count]);
        var blocks = new[]
        {
            Block(entry, [], [
                Instruction.Factory.Load(default, new LoadOperation(), loadedChoose, choose.Value),
                Instruction.Factory.Load(default, new LoadOperation(), loadedCount, count.Value)
            ], Terms.Br(new(header, [Int(0), loadedCount, loadedChoose]))),
            Block(header, [state, remaining, selector], [
                GreaterThan(active, remaining, Int(0))
            ], Terms.BrIf(
                active,
                new(work, [state, remaining, selector]),
                new(exitChoice, [state]))),
            Block(work, [workState, workRemaining, workSelector], [],
                Terms.BrIf(
                    workSelector,
                    new(left, [workState, workRemaining, workSelector]),
                    new(right, [workState, workRemaining, workSelector]))),
            Block(left, [leftState, leftRemaining, leftSelector], [
                Add(leftNextState, leftState, Int(1)),
                Subtract(leftNextRemaining, leftRemaining, Int(1)),
                GreaterThan(leftAgain, leftNextRemaining, Int(0))
            ], Terms.BrIf(
                leftAgain,
                new(header, [leftNextState, leftNextRemaining, leftSelector]),
                new(firstExit, [leftNextState]))),
            Block(right, [rightState, rightRemaining, rightSelector], [
                Add(rightNextState, rightState, Int(1)),
                Subtract(rightNextRemaining, rightRemaining, Int(1))
            ], Terms.Br(new(header, [rightNextState, rightNextRemaining, rightSelector]))),
            Block(exitChoice, [exitState], [], Terms.Br(new(secondExit, [exitState]))),
            Block(firstExit, [firstState], [
                Add(firstAnswer, firstState, Int(10))
            ], Terms.ReturnExpr(firstAnswer)),
            Block(secondExit, [secondState], [
                Add(secondAnswer, secondState, Int(20))
            ], Terms.ReturnExpr(secondAnswer))
        };
        return Case("hand-multiple-latches-exits", declaration, [.. blocks], entry);
    }

    internal static RawCase NestedOneTripLoops(int depth)
    {
        var entry = Label.Create($"depth-{depth}-entry");
        var headers = Enumerable.Range(0, depth)
            .Select(index => Label.Create($"depth-{depth}-header-{index}"))
            .ToImmutableArray();
        var latches = Enumerable.Range(0, depth)
            .Select(index => Label.Create($"depth-{depth}-latch-{index}"))
            .ToImmutableArray();
        var inner = Label.Create($"depth-{depth}-inner");
        var exit = Label.Create($"depth-{depth}-exit");
        var state = new VariableDeclaration(FunctionAddressSpace.Instance, "state", ShaderType.I32, []);
        var counters = Enumerable.Range(0, depth)
            .Select(index => new VariableDeclaration(
                FunctionAddressSpace.Instance,
                $"counter{index}",
                ShaderType.I32,
                []))
            .ToImmutableArray();
        var blocks = new List<CilValueBasicBlock>();
        var entryInstructions = ImmutableArray.CreateBuilder<Instruction<IShaderValue, IShaderValue>>();
        entryInstructions.Add(Instruction.Factory.Store(default, new StoreOperation(), state.Value, Int(0)));
        foreach (var counter in counters)
            entryInstructions.Add(Instruction.Factory.Store(default, new StoreOperation(), counter.Value, Int(1)));
        blocks.Add(Block(entry, [], entryInstructions, Terms.Br(new(headers[0], []))));

        for (var index = 0; index < depth; index++)
        {
            var counter = ShaderValue.Intermediate(ShaderType.I32);
            var active = ShaderValue.Intermediate(ShaderType.Bool);
            var whenTrue = index == depth - 1 ? inner : headers[index + 1];
            var whenFalse = index == 0 ? exit : latches[index - 1];
            blocks.Add(Block(headers[index], [], [
                Instruction.Factory.Load(default, new LoadOperation(), counter, counters[index].Value),
                GreaterThan(active, counter, Int(0))
            ], Terms.BrIf(active, new(whenTrue, []), new(whenFalse, []))));
        }

        var before = ShaderValue.Intermediate(ShaderType.I32);
        var after = ShaderValue.Intermediate(ShaderType.I32);
        blocks.Add(Block(inner, [], [
            Instruction.Factory.Load(default, new LoadOperation(), before, state.Value),
            Add(after, before, Int(1)),
            Instruction.Factory.Store(default, new StoreOperation(), state.Value, after)
        ], Terms.Br(new(latches[^1], []))));

        for (var index = 0; index < depth; index++)
        {
            var beforeCounter = ShaderValue.Intermediate(ShaderType.I32);
            var afterCounter = ShaderValue.Intermediate(ShaderType.I32);
            blocks.Add(Block(latches[index], [], [
                Instruction.Factory.Load(default, new LoadOperation(), beforeCounter, counters[index].Value),
                Subtract(afterCounter, beforeCounter, Int(1)),
                Instruction.Factory.Store(default, new StoreOperation(), counters[index].Value, afterCounter)
            ], Terms.Br(new(headers[index], []))));
        }

        var answer = ShaderValue.Intermediate(ShaderType.I32);
        blocks.Add(Block(exit, [], [
            Instruction.Factory.Load(default, new LoadOperation(), answer, state.Value)
        ], Terms.ReturnExpr(answer)));
        return Case(
            $"hand-depth-{depth}",
            Function($"NestedOneTrip{depth}", []),
            [.. blocks],
            entry,
            [state, .. counters]);
    }

    internal static ImmutableArray<RawCase> IrreducibleControls()
    {
        var firstEntry = Label.Create("two-entry-root");
        var firstLeft = Label.Create("two-entry-left");
        var firstRight = Label.Create("two-entry-right");
        var twoEntry = Case(
            "reject-two-entry-cycle",
            Function("TwoEntryCycle", []),
            [
                Block(firstEntry, [], [], Terms.BrIf(
                    Bool(true),
                    new(firstLeft, []),
                    new(firstRight, []))),
                Block(firstLeft, [], [], Terms.Br(new(firstRight, []))),
                Block(firstRight, [], [], Terms.Br(new(firstLeft, [])))
            ],
            firstEntry);

        var sideEntry = Label.Create("side-root");
        var header = Label.Create("side-header");
        var side = Label.Create("side-entry");
        var body = Label.Create("side-body");
        var exit = Label.Create("side-exit");
        var sideCycle = Case(
            "reject-side-entry-cycle",
            Function("SideEntryCycle", []),
            [
                Block(sideEntry, [], [], Terms.BrIf(Bool(true), new(header, []), new(side, []))),
                Block(header, [], [], Terms.Br(new(body, []))),
                Block(side, [], [], Terms.Br(new(body, []))),
                Block(body, [], [], Terms.BrIf(Bool(true), new(header, []), new(exit, []))),
                Block(exit, [], [], Terms.ReturnExpr(Int(0)))
            ],
            sideEntry);
        return [twoEntry, sideCycle];
    }

    internal static CompiledCase Compile(RawCase raw, Capture capture)
    {
        capture.Write("input.txt", raw.Input);
        capture.Write("identity.txt", FormatIdentity(raw));
        var facts = Stage(
            "control-facts",
            capture,
            () => Annotate(raw));
        capture.Write("facts.txt", facts.PrettyPrint());
        var tree = Stage(
            "region-tree",
            capture,
            () => RegionTree.Create(
                facts,
                static (label, block, control) => new ShaderRegionBody(
                    label,
                    block.Parameters,
                    block.Body,
                    control.PostDominance)));
        var region = Stage("checked-region-body", capture, () => new RegionFunctionBody(raw.Declaration, tree));
        capture.Write("region.txt", region.Dump());
        var normalized = Stage(
            "operation-normalization",
            capture,
            () => new StablePointerRegionParameterPass().VisitFunctionBody(
                new FunctionToOperationPass().VisitFunctionBody(region)));
        capture.Write("normalized-region.txt", normalized.Dump());
        var target = Stage("target-lowering", capture, () => ScalarControlFlowTests.Lower(normalized));
        capture.Write("target-ast.txt", target.PrettyPrint());
        var source = Stage("source-emission", capture, () => ScalarControlFlowTests.Emit(target));
        capture.Write("target.slang", source);
        return new CompiledCase(raw, facts, region, normalized, target, source);
    }

    internal static ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>> Annotate(RawCase raw) =>
        ControlFlowFacts.Annotate(
            raw.Graph,
            CilStagePrettyPrinter.CreateValueBlockControlFactsPrinter(raw.Context));

    internal static Execution RunRaw(
        RawCase raw,
        ImmutableArray<Value> arguments,
        int stepLimit = StepLimit) =>
        RunCfg(raw.Graph, raw.Declaration, arguments, raw.Locals, raw.InitLocals, stepLimit);

    internal static (int Depth, ImmutableArray<Label> Headers) MaximumLoopDepth(
        RegionTree<Label, ShaderRegionBody> root)
    {
        var best = ImmutableArray<Label>.Empty;

        void Visit(RegionTree<Label, ShaderRegionBody> region, ImmutableArray<Label> ancestors)
        {
            var current = region.Definition.Kind is RegionKind.Loop
                ? ancestors.Add(region.Label)
                : ancestors;
            if (current.Length > best.Length)
                best = current;
            foreach (var child in region.Bindings)
                Visit(child, current);
        }

        Visit(root, []);
        return (best.Length, best);
    }

    internal sealed class Capture
    {
        private readonly string? directory;

        internal Capture(string caseId, int stepLimit = StepLimit)
        {
            var root = Environment.GetEnvironmentVariable("DRILLA_E2_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(root))
                return;
            directory = Path.Combine(root, caseId);
            Directory.CreateDirectory(directory);
            var configuration = typeof(ControlFlowCorpus).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown";
            Write(
                "metadata.txt",
                $"case={caseId}{Environment.NewLine}" +
                $"configuration={configuration}{Environment.NewLine}" +
                $"sourceSHA={Environment.GetEnvironmentVariable("DRILLA_E2_SOURCE_SHA") ?? "unspecified"}{Environment.NewLine}" +
                $"sourceSHAStatus={Environment.GetEnvironmentVariable("DRILLA_E2_SOURCE_SHA_STATUS") ?? "development"}{Environment.NewLine}" +
                $"command={Environment.GetEnvironmentVariable("DRILLA_E2_REPRO_COMMAND") ?? "unspecified"}{Environment.NewLine}" +
                $"stepLimit={stepLimit}{Environment.NewLine}");
        }

        internal void Write(string name, string content)
        {
            if (directory is null)
                return;
            var path = Path.Combine(directory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("Capture path has no directory."));
            File.WriteAllText(path, content);
        }
    }

    private static RawCase Case(
        string id,
        FunctionDeclaration declaration,
        ImmutableArray<CilValueBasicBlock> blocks,
        Label entry,
        ImmutableArray<VariableDeclaration> locals = default)
    {
        var graph = Graph(entry, blocks);
        return CreateRawCase(
            id,
            declaration,
            graph,
            locals.IsDefault ? [] : locals,
            false,
            string.Empty);
    }

    private static RawCase CreateRawCase(
        string id,
        FunctionDeclaration declaration,
        ControlFlowGraph<CilValueBasicBlock> graph,
        ImmutableArray<VariableDeclaration> locals,
        bool initLocals,
        string preamble,
        ImmutableDictionary<Label, int>? stableIdentity = null)
    {
        var labels = graph.Labels().ToImmutableArray();
        var context = new CilStageDeclarationContext(
            locals,
            labels,
            labels.SelectMany(label => Values(graph[label]))
                .Distinct<IShaderValue>(ReferenceEqualityComparer.Instance)
                .ToImmutableArray());
        var identity = stableIdentity ?? labels.Select((label, index) => KeyValuePair.Create(label, index))
            .ToImmutableDictionary();
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(text);
        writer.Write(preamble);
        writer.Write("function ");
        writer.Write(declaration.ReturnType.Name);
        writer.Write(" ");
        writer.Write(declaration.Name);
        writer.Write("(");
        writer.Write(string.Join(", ", declaration.Parameters.Select(parameter =>
            $"{parameter.Type.Name} {parameter.Name}")));
        writer.WriteLine(")");
        writer.WriteLine($"locals=[{string.Join(", ", locals.Select(local => $"{local.Type.Name} {local.Name}"))}]");
        CilStagePrettyPrinter.PrintValueControlFlow(graph, context, writer);
        return new RawCase(id, declaration, graph, locals, initLocals, context, identity, text.ToString());
    }

    private static IEnumerable<IShaderValue> Values(CilValueBasicBlock block)
    {
        foreach (var parameter in block.Parameters)
            yield return parameter;
        foreach (var instruction in block.Body.Elements)
        {
            if (instruction.Result is { } result)
                yield return result;
            foreach (var operand in instruction.Operands)
                yield return operand;
        }
        foreach (var value in block.Body.Last.Evaluate(TerminatorValues.Instance))
            yield return value;
    }

    private static string FormatIdentity(RawCase raw) =>
        string.Join(
            Environment.NewLine,
            raw.Identity.OrderBy(item => item.Value)
                .Select(item => $"{item.Value}={item.Key.Name}")) + Environment.NewLine;

    private static T Stage<T>(string name, Capture capture, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception error)
        {
            capture.Write($"failure-{name}.txt", error.ToString());
            throw new CorpusStageException(name, error);
        }
    }

    internal sealed class CorpusStageException(string stage, Exception innerException)
        : Exception($"Corpus stage '{stage}' failed: {innerException.Message}", innerException)
    {
        internal string Stage { get; } = stage;
    }

    private static ControlFlowGraph<CilValueBasicBlock> Graph(
        Label entry,
        IEnumerable<CilValueBasicBlock> blocks) =>
        new(
            entry,
            blocks.ToDictionary(
                static block => block.Label,
                static block => new ControlFlowGraph<CilValueBasicBlock>.NodeDefinition(
                    block.Successor,
                    block)));

    private static CilValueBasicBlock Block(
        Label label,
        ImmutableArray<IShaderValue> parameters,
        IEnumerable<Instruction<IShaderValue, IShaderValue>> instructions,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        new(label, parameters, Seq.Create([.. instructions], terminator));

    private static FunctionDeclaration Function(
        string name,
        ImmutableArray<ParameterDeclaration> parameters) =>
        new(name, parameters, new FunctionReturn(ShaderType.I32, []), []);

    private static Instruction<IShaderValue, IShaderValue> Add(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
            result,
            left,
            right);

    private static Instruction<IShaderValue, IShaderValue> Subtract(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Sub>.Instance,
            result,
            left,
            right);

    private static Instruction<IShaderValue, IShaderValue> Multiply(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Mul>.Instance,
            result,
            left,
            right);

    private static Instruction<IShaderValue, IShaderValue> GreaterThan(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>.Instance,
            result,
            left,
            right);

    private static IShaderValue Int(int value) => ShaderValue.Literal(new I32Literal(value));
    private static IShaderValue Bool(bool value) => ShaderValue.Literal(new BoolLiteral(value));

    private static IEnumerable<ImmutableArray<int>> ForwardOptions(int node, int nodeCount)
    {
        yield return [];
        for (var target = node + 1; target < nodeCount; target++)
            yield return [target];
        for (var whenTrue = node + 1; whenTrue < nodeCount; whenTrue++)
            for (var whenFalse = node + 1; whenFalse < nodeCount; whenFalse++)
                yield return [whenTrue, whenFalse];
    }

    private static bool AllReachable(ImmutableArray<ImmutableArray<int>> targets)
    {
        var visited = new HashSet<int> { 0 };
        var pending = new Stack<int>();
        pending.Push(0);
        while (pending.TryPop(out var source))
            foreach (var target in targets[source])
                if (visited.Add(target))
                    pending.Push(target);
        return visited.Count == targets.Length;
    }

    private static Dictionary<int, int> PathChoices(GraphSpec spec, int target)
    {
        var predecessor = new (int Source, int Arm)?[spec.NodeCount];
        var pending = new Queue<int>();
        var visited = new bool[spec.NodeCount];
        visited[0] = true;
        pending.Enqueue(0);
        while (pending.TryDequeue(out var source) && !visited[target])
            for (var arm = 0; arm < spec.Targets[source].Length; arm++)
            {
                var next = spec.Targets[source][arm];
                if (visited[next])
                    continue;
                visited[next] = true;
                predecessor[next] = (source, arm);
                pending.Enqueue(next);
            }

        if (!visited[target])
            throw new InvalidOperationException($"{spec.Id}: node {target} is unreachable.");
        var choices = new Dictionary<int, int>();
        for (var current = target; current != 0;)
        {
            var edge = predecessor[current]
                ?? throw new InvalidOperationException($"{spec.Id}: missing predecessor for {current}.");
            choices[edge.Source] = edge.Arm;
            current = edge.Source;
        }
        return choices;
    }

    private sealed class TerminatorValues
        : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, ImmutableArray<IShaderValue>>
    {
        internal static TerminatorValues Instance { get; } = new();

        public ImmutableArray<IShaderValue> ReturnVoid() => [];
        public ImmutableArray<IShaderValue> ReturnExpr(IShaderValue expr) => [expr];
        public ImmutableArray<IShaderValue> Br(RegionJump<IShaderValue> target) => target.Arguments;
        public ImmutableArray<IShaderValue> BrIf(
            IShaderValue condition,
            RegionJump<IShaderValue> trueTarget,
            RegionJump<IShaderValue> falseTarget) =>
            [condition, .. trueTarget.Arguments, .. falseTarget.Arguments];
        public ImmutableArray<IShaderValue> Switch(
            IShaderValue selector,
            IReadOnlyList<RegionJump<IShaderValue>> caseTargets,
            RegionJump<IShaderValue> defaultTarget) =>
            [selector, .. caseTargets.SelectMany(target => target.Arguments), .. defaultTarget.Arguments];
    }

    private sealed class StableRandom(uint state)
    {
        private uint state = state;

        internal int Next(int exclusiveMaximum)
        {
            state = unchecked(state * 1_664_525u + 1_013_904_223u);
            return (int)(state % (uint)exclusiveMaximum);
        }
    }
}
