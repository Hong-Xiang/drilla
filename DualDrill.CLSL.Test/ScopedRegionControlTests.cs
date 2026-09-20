using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Globalization;
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
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using static DualDrill.CLSL.Test.ScopedContinuationOracle;

namespace DualDrill.CLSL.Test;

public sealed class ScopedRegionControlTests(ITestOutputHelper output)
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Fact]
    public void SameTargetArmsKeepDistinctTuplesAndExecuteSharedEffectOnce()
    {
        var body = ScalarControlFlowTests.HandBody();

        foreach (var (input, expected) in new[] { (1, 11), (-1, 21) })
        {
            var arguments = ImmutableArray.Create<Value>(new Value.Integer(input));
            var cfg = RunCfg(body, arguments);
            var scoped = RunScoped(body, arguments);

            Assert.Equal(new Value.Integer(expected), scoped.Result);
            Assert.Equal(cfg.Result, scoped.Result);
            Assert.True(cfg.Trace.SequenceEqual(scoped.Trace));
            output.WriteLine(
                $"ACTUAL input={input} result={scoped.Result} trace={string.Join(" -> ", scoped.Trace.Select(LabelName))}");
        }

        var entryTransfers = body.Control.Transfers.Where(transfer => ReferenceEquals(transfer.Source, body.Entry));
        Assert.Equal([0, 1], entryTransfers.Select(transfer => transfer.Arm));
        Assert.All(entryTransfers, transfer => Assert.Equal("tail", transfer.Target.Name));
        Assert.Equal(
            """
            scope entry: [exit forward owner=entry, tail forward owner=entry]
            scope exit: []
            scope tail: [exit forward owner=entry]
            transfer entry#0 -> tail forward owner=entry
            transfer entry#1 -> tail forward owner=entry
            transfer tail#0 -> exit forward owner=entry

            """,
            FormatControl(body));
        output.WriteLine("ACTUAL scoped control:");
        output.WriteLine(body.Dump());
    }

    [Fact]
    public void CrossScopeTransfersRetainTheirDefiningOwners()
    {
        var entry = Label.Create("entry");
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var afterInner = Label.Create("after-inner");
        var done = Label.Create("done");
        var repeat = ShaderValue.Intermediate(ShaderType.Bool);
        var body = Function(
            RegionTree.Block(entry, [
                RegionTree.Block(done, [], Body(done,
                    Terms.ReturnExpr(ShaderValue.Literal(new I32Literal(0))),
                    new ExitPostDominance.FunctionExit(false)), null),
                RegionTree.Loop(outer, [
                    RegionTree.Block(afterInner, [], Body(
                        afterInner,
                        Terms.Br(new(done, [])),
                        new ExitPostDominance.Block(done, false)), null),
                    RegionTree.Loop(inner, [], Body(inner,
                        Terms.BrIf(repeat,
                            new(outer, [ShaderValue.Literal(new BoolLiteral(false))]),
                            new(afterInner, [])),
                        new ExitPostDominance.Block(afterInner, true)), null, null)
                ], Body(
                    outer,
                    Terms.Br(new(inner, [])),
                    new ExitPostDominance.Block(inner, true),
                    [repeat]), null, null)
            ], Body(
                entry,
                Terms.Br(new(outer, [ShaderValue.Literal(new BoolLiteral(true))])),
                new ExitPostDominance.Block(outer, true)), null),
            ShaderType.I32);

        var outerRepeat = body.Control.Resolve(inner, 0);
        var innerExit = body.Control.Resolve(inner, 1);
        var outerExit = body.Control.Resolve(afterInner, 0);

        Assert.Equal(ScopedContinuationKind.Repeat, outerRepeat.Kind);
        Assert.Same(outer, outerRepeat.Owner);
        Assert.Equal(ScopedContinuationKind.Forward, innerExit.Kind);
        Assert.Same(outer, innerExit.Owner);
        Assert.Equal(ScopedContinuationKind.Forward, outerExit.Kind);
        Assert.Same(entry, outerExit.Owner);
        var expectedTrace = new[] { entry, outer, inner, outer, inner, afterInner, done };
        var cfg = RunCfg(body, []);
        var scoped = RunScoped(body, []);
        Assert.Equal(new Value.Integer(0), scoped.Result);
        Assert.Equal(cfg.Result, scoped.Result);
        Assert.True(expectedTrace.SequenceEqual(cfg.Trace));
        Assert.True(expectedTrace.SequenceEqual(scoped.Trace));
        output.WriteLine(
            $"ACTUAL outer transfer CFG result={cfg.Result} trace={string.Join(" -> ", cfg.Trace.Select(LabelName))}");
        output.WriteLine(
            $"ACTUAL outer transfer scoped result={scoped.Result} " +
            $"trace={string.Join(" -> ", scoped.Trace.Select(LabelName))}");
        output.WriteLine("ACTUAL outer transfer region/control:");
        output.WriteLine(body.Dump());
    }

    [Theory]
    [InlineData(true, 42, "entry -> left -> join")]
    [InlineData(false, 43, "entry -> right -> join")]
    public void DominatingValuesRemainAvailableToDynamicDescendants(
        bool chooseLeft,
        int expected,
        string expectedTrace)
    {
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var join = Label.Create("join");
        var choose = new ParameterDeclaration("choose", ShaderType.Bool, []);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var value = ShaderValue.Intermediate(ShaderType.I32);
        var incremented = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var body = new RegionFunctionBody(
            new FunctionDeclaration("DominatingValue", [choose], new FunctionReturn(ShaderType.I32, []), []),
            RegionTree.Block(entry, [
                RegionTree.Block(join, [], Body(
                    join,
                    Terms.ReturnExpr(result),
                    new ExitPostDominance.FunctionExit(false),
                    [result]), null),
                RegionTree.Block(left, [], Body(
                    left,
                    Terms.Br(new(join, [value])),
                    new ExitPostDominance.Block(join, false)), null),
                RegionTree.Block(right, [], ShaderRegionBody.Create(right, [], [
                    Instruction.Factory.Operation2(default,
                        NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                        incremented,
                        value,
                        ShaderValue.Literal(new I32Literal(1)))
                ], Terms.Br(new(join, [incremented])),
                    new ExitPostDominance.Block(join, false)), null)
            ], ShaderRegionBody.Create(entry, [], [
                Instruction.Factory.Load(default, new LoadOperation(), condition, choose.Value),
                Instruction.Factory.Literal(default, new LiteralOperation(), value,
                    ShaderValue.Literal(new I32Literal(42)))
            ], Terms.BrIf(condition, new(left, []), new(right, [])),
                new ExitPostDominance.Block(join, false)), null));
        var arguments = ImmutableArray.Create<Value>(new Value.Boolean(chooseLeft));

        var cfg = RunCfg(body, arguments);
        var scoped = RunScoped(body, arguments);
        Assert.Equal(new Value.Integer(expected), cfg.Result);
        Assert.Equal(cfg.Result, scoped.Result);
        Assert.Equal(expectedTrace, string.Join(" -> ", cfg.Trace.Select(LabelName)));
        Assert.True(cfg.Trace.SequenceEqual(scoped.Trace));
        output.WriteLine(
            $"ACTUAL dominating value input={chooseLeft} CFG result={cfg.Result} " +
            $"trace={string.Join(" -> ", cfg.Trace.Select(LabelName))}");
        output.WriteLine(
            $"ACTUAL dominating value input={chooseLeft} scoped result={scoped.Result} " +
            $"trace={string.Join(" -> ", scoped.Trace.Select(LabelName))}");
        output.WriteLine("ACTUAL dominating value region/control:");
        output.WriteLine(body.Dump());
    }

    [Fact]
    public void CheckedBoundaryRejectsInvalidScopedBodies()
    {
        var entry = Label.Create("entry");
        var first = Label.Create("first");
        var second = Label.Create("second");
        var privateLabel = Label.Create("private");
        var unknown = Label.Create("unknown");
        var parameter = ShaderValue.Intermediate(ShaderType.I32);

        Rejects("not visible", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(
                first,
                Terms.Br(new(second, [])),
                new ExitPostDominance.Block(second, false)), null),
            RegionTree.Block(second, [], Body(
                second,
                Terms.ReturnVoid(),
                new ExitPostDominance.FunctionExit(false)), null)
        ], Body(entry, Terms.Br(new(first, [])), new ExitPostDominance.Block(first, false)), null));

        Rejects("not visible", RegionTree.Block(entry, [
            RegionTree.Block(first, [
                RegionTree.Block(privateLabel, [], Body(
                    privateLabel,
                    Terms.ReturnVoid(),
                    new ExitPostDominance.FunctionExit(false)), null)
            ], Body(
                first,
                Terms.Br(new(privateLabel, [])),
                new ExitPostDominance.Block(privateLabel, false)), null),
            RegionTree.Block(second, [], Body(
                second,
                Terms.Br(new(privateLabel, [])),
                new ExitPostDominance.Block(privateLabel, false)), null)
        ], Body(entry, Terms.Br(new(second, [])), new ExitPostDominance.Block(second, false)), null));

        Rejects("block self-reference",
            RegionTree.Block(entry, [], Body(
                entry,
                Terms.Br(new(entry, [])),
                ExitPostDominance.NoExitPath.Instance), null));
        Rejects("unknown label",
            RegionTree.Block(entry, [], Body(
                entry,
                Terms.Br(new(unknown, [])),
                ExitPostDominance.NoExitPath.Instance), null));
        Rejects("duplicate defined label", RegionTree.Block(entry, [
            RegionTree.Block(entry, [], Body(
                entry,
                Terms.ReturnVoid(),
                new ExitPostDominance.FunctionExit(false)), null)
        ], Body(entry, Terms.ReturnVoid(), new ExitPostDominance.FunctionExit(false)), null));
        Rejects("does not match body label",
            RegionTree.Block(entry, [], Body(
                first,
                Terms.ReturnVoid(),
                new ExitPostDominance.FunctionExit(false)), null));
        Rejects("incorrect argument count", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(
                first,
                Terms.ReturnVoid(),
                new ExitPostDominance.FunctionExit(false),
                [parameter]), null)
        ], Body(entry, Terms.Br(new(first, [])), new ExitPostDominance.Block(first, false)), null));
        Rejects("incorrect argument type", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(
                first,
                Terms.ReturnVoid(),
                new ExitPostDominance.FunctionExit(false),
                [parameter]), null)
        ], Body(
            entry,
            Terms.Br(new(first, [ShaderValue.Literal(new BoolLiteral(true))])),
            new ExitPostDominance.Block(first, false)), null));
        Rejects("entry region", RegionTree.Block(entry, [],
            Body(entry, Terms.ReturnVoid(), new ExitPostDominance.FunctionExit(false), [parameter]), null));
        Rejects("unreachable definitions", RegionTree.Block(entry, [
            RegionTree.Block(first, [], Body(
                first,
                Terms.ReturnVoid(),
                new ExitPostDominance.FunctionExit(false)), null)
        ], Body(entry, Terms.ReturnVoid(), new ExitPostDominance.FunctionExit(false)), null));
    }

    [Fact]
    public void CheckedBoundaryRejectsDefaultRootParameterArray()
    {
        var entry = Label.Create("entry");

        Rejects(
            "Function 'Scoped': region 'Label(entry)' has a default parameter array",
            RegionTree.Block(entry, [],
                ShaderRegionBody.Create(
                    entry,
                    default,
                    [],
                    Terms.ReturnVoid(),
                    new ExitPostDominance.FunctionExit(false)), null));
    }

    [Fact]
    public void CheckedBoundaryRejectsDefaultNonrootParameterArray()
    {
        var entry = Label.Create("entry");
        var child = Label.Create("child");

        Rejects(
            "Function 'Scoped': region 'Label(child)' has a default parameter array",
            RegionTree.Block(entry, [
                RegionTree.Block(child, [],
                    ShaderRegionBody.Create(
                        child,
                        default,
                        [],
                        Terms.ReturnVoid(),
                        new ExitPostDominance.FunctionExit(false)), null)
            ], Body(entry, Terms.Br(new(child, [])), new ExitPostDominance.Block(child, false)), null));
    }

    [Fact]
    public void CheckedBoundaryRejectsDefaultJumpArgumentArray()
    {
        var entry = Label.Create("entry");
        var child = Label.Create("child");

        Rejects(
            "Function 'Scoped': transfer from 'Label(entry)', arm 0, to 'Label(child)' " +
            "has a default argument array",
            RegionTree.Block(entry, [
                RegionTree.Block(child, [], Body(
                    child,
                    Terms.ReturnVoid(),
                    new ExitPostDominance.FunctionExit(false)), null)
            ], Body(
                entry,
                Terms.Br(new RegionJump<IShaderValue>(child, default)),
                new ExitPostDominance.Block(child, false)), null));
    }

    [Fact]
    public void CheckedBoundaryRejectsMissingBlockParameterType()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var parameter = ShaderValue.Intermediate(null!, "bad");

        Rejects(
            "Function 'Scoped': region 'Label(exit)' parameter at index 0 type is missing",
            RegionTree.Block(entry, [
                RegionTree.Block(exit, [],
                    Body(
                        exit,
                        Terms.ReturnVoid(),
                        new ExitPostDominance.FunctionExit(false),
                        [parameter]), null)
            ], Body(
                entry,
                Terms.Br(new(exit, [ShaderValue.Literal(new I32Literal(0))])),
                new ExitPostDominance.Block(exit, false)), null));
    }

    [Fact]
    public void LoopSelfRepeatIsCheckedAndBudgeted()
    {
        var loop = Label.Create("loop");
        var body = Function(
            RegionTree.Loop(loop, [], Body(
                loop,
                Terms.Br(new(loop, [])),
                ExitPostDominance.NoExitPath.Instance), null, null),
            ShaderType.I32);

        var transfer = body.Control.Resolve(loop, 0);
        Assert.Equal(ScopedContinuationKind.Repeat, transfer.Kind);
        Assert.Same(loop, transfer.Owner);
        var error = Assert.Throws<InvalidOperationException>(() => RunScoped(body, [], 8));
        Assert.Contains("step budget", error.Message);
        output.WriteLine($"ACTUAL nontermination rejection: {error.Message}");
    }

    [Fact]
    public void StructurallyEquivalentPointerTypesAreAccepted()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var source = new ParameterDeclaration("source", ShaderType.I32, []);
        var parameter = ShaderValue.Intermediate(
            new PtrType(ShaderType.I32, FunctionAddressSpace.Instance));
        var body = new RegionFunctionBody(
            new FunctionDeclaration("Pointers", [source], new FunctionReturn(UnitType.Instance, []), []),
            RegionTree.Block(entry, [
                RegionTree.Block(target, [], Body(
                    target,
                    Terms.ReturnVoid(),
                    new ExitPostDominance.FunctionExit(false),
                    [parameter]), null)
            ], Body(
                entry,
                Terms.Br(new(target, [source.Value])),
                new ExitPostDominance.Block(target, false)), null));

        Assert.Same(target, body.Control.Resolve(entry, 0).Target);
    }

    [Fact]
    public void GenericRegionConstructionRejectsIrreducibleSiblingCycle()
    {
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var graph = new ControlFlowGraph<ISuccessor>(
            entry,
            new Dictionary<Label, ControlFlowGraph<ISuccessor>.NodeDefinition>
            {
                [entry] = new(new ConditionalSuccessor(left, right), new ConditionalSuccessor(left, right)),
                [left] = new(new UnconditionalSuccessor(right), new UnconditionalSuccessor(right)),
                [right] = new(new UnconditionalSuccessor(left), new UnconditionalSuccessor(left))
            });
        var annotated = ControlFlowFacts.Annotate(
            graph,
            static (_, _, _, _) => { });

        var error = Assert.Throws<ArgumentException>(() =>
            RegionTree.Create(annotated, static (_, body, _) => body));

        Assert.Contains("lexical scope", error.Message);
    }

    [Fact]
    public void ThreeNodeRegionConstructionMatchesIndependentReducibilityOracle()
    {
        var labels = new[] { Label.Create("n0"), Label.Create("n1"), Label.Create("n2") };
        var options = new List<int[]> { Array.Empty<int>() };
        for (var target = 0; target < labels.Length; target++)
            options.Add([target]);
        for (var whenTrue = 0; whenTrue < labels.Length; whenTrue++)
            for (var whenFalse = 0; whenFalse < labels.Length; whenFalse++)
                options.Add([whenTrue, whenFalse]);

        var checkedGraphs = 0;
        foreach (var first in options)
            foreach (var second in options)
                foreach (var third in options)
                {
                    int[][] successors = [first, second, third];
                    if (!AllReachable(successors))
                        continue;

                    checkedGraphs++;
                    var graph = new ControlFlowGraph<ISuccessor>(
                        labels[0],
                        labels.Select((label, index) =>
                        {
                            var successor = Successor(successors[index], labels);
                            return KeyValuePair.Create(
                                label,
                                new ControlFlowGraph<ISuccessor>.NodeDefinition(successor, successor));
                        }).ToDictionary());
                    var annotated = ControlFlowFacts.Annotate(graph, static (_, _, _, _) => { });
                    string? rejection = null;
                    try
                    {
                        _ = RegionTree.Create(annotated, static (_, body, _) => body);
                    }
                    catch (ArgumentException error)
                    {
                        rejection = error.Message;
                    }

                    var expected = IsReducible(successors);
                    var actual = rejection is null;
                    Assert.True(
                        expected == actual,
                        $"Minimal counterexample: {Describe(successors)}; independent={expected}; " +
                        $"production={(actual ? "accepted" : $"rejected: {rejection}")}");
                }

        Assert.Equal(962, checkedGraphs);
        output.WriteLine($"ACTUAL checked {checkedGraphs} reachable ordered three-node successor graphs.");
    }

    private static string LabelName(Label label) => label.Name ?? "<unnamed>";

    private static string FormatControl(RegionFunctionBody body)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(text);
        body.Control.Dump(writer, LabelName);
        return text.ToString();
    }

    private void Rejects(string expected, RegionTree<Label, ShaderRegionBody> tree)
    {
        var error = Assert.Throws<ArgumentException>(() => Function(tree));
        Assert.Contains(expected, error.Message);
        output.WriteLine($"ACTUAL rejection: {error.Message}");
    }

    private static RegionFunctionBody Function(
        RegionTree<Label, ShaderRegionBody> tree,
        IShaderType? returnType = null) =>
        new(
            new FunctionDeclaration(
                "Scoped",
                [],
                new FunctionReturn(returnType ?? UnitType.Instance, []),
                []),
            tree);

    private static ShaderRegionBody Body(
        Label label,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
        ExitPostDominance postDominance,
        ImmutableArray<IShaderValue> parameters = default) =>
        ShaderRegionBody.Create(
            label,
            parameters.IsDefault ? [] : parameters,
            [],
            terminator,
            postDominance);

    private static ISuccessor Successor(int[] targets, Label[] labels) =>
        targets.Length switch
        {
            0 => new TerminateSuccessor(),
            1 => new UnconditionalSuccessor(labels[targets[0]]),
            2 => new ConditionalSuccessor(labels[targets[0]], labels[targets[1]]),
            _ => throw new ArgumentOutOfRangeException(nameof(targets))
        };

    private static bool AllReachable(int[][] successors) =>
        Enumerable.Range(0, successors.Length).All(node => ReachableWithout(successors, node, -1));

    private static bool IsReducible(int[][] successors)
    {
        var retained = successors
            .Select((targets, source) =>
                targets.Where(target => !Dominates(successors, target, source)).ToArray())
            .ToArray();
        var indegree = new int[successors.Length];
        foreach (var targets in retained)
            foreach (var target in targets)
                indegree[target]++;

        var pending = new Queue<int>(
            Enumerable.Range(0, indegree.Length).Where(node => indegree[node] == 0));
        var visited = 0;
        while (pending.TryDequeue(out var source))
        {
            visited++;
            foreach (var target in retained[source])
                if (--indegree[target] == 0)
                    pending.Enqueue(target);
        }

        return visited == successors.Length;
    }

    private static bool Dominates(int[][] successors, int candidate, int node) =>
        candidate == node || !ReachableWithout(successors, node, candidate);

    private static bool ReachableWithout(int[][] successors, int target, int excluded)
    {
        if (excluded == 0)
            return false;
        var visited = new bool[successors.Length];
        var pending = new Stack<int>();
        pending.Push(0);
        while (pending.TryPop(out var source))
        {
            if (source == excluded || visited[source])
                continue;
            visited[source] = true;
            foreach (var next in successors[source])
                if (next != excluded)
                    pending.Push(next);
        }

        return visited[target];
    }

    private static string Describe(int[][] successors) =>
        string.Join(
            "; ",
            successors.Select((targets, source) =>
                $"{source}: " + (targets.Length switch
                {
                    0 => "return",
                    1 => $"br {targets[0]}",
                    _ => $"br_if {targets[0]} {targets[1]}"
                })));
}
