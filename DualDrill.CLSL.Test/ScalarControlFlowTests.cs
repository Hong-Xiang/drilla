using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
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
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using static DualDrill.CLSL.Test.ScopedContinuationOracle;
using static DualDrill.CLSL.Test.RegionFixture;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class ScalarControlFlowTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("SharedTail", -2, 96)]
    [InlineData("SharedTail", 0, 98)]
    [InlineData("SharedTail", 2, 58)]
    [InlineData("MultipleReturns", -2, -9)]
    [InlineData("MultipleReturns", 0, 31)]
    [InlineData("MultipleReturns", 2, 15)]
    [InlineData("MinimumLoop", -1, 0)]
    [InlineData("MinimumLoop", 0, 0)]
    [InlineData("MinimumLoop", 1, 1)]
    [InlineData("MinimumLoop", 4, 4)]
    [InlineData("ContinueAndBreak", 0, 18)]
    [InlineData("ContinueAndBreak", 2, 27)]
    [InlineData("ContinueAndBreak", 4, 50)]
    [InlineData("ContinueAndBreak", 8, 120)]
    [InlineData("LoopCarriedSwap", 0, 13)]
    [InlineData("LoopCarriedSwap", 1, 23)]
    [InlineData("LoopCarriedSwap", 4, 39)]
    [InlineData("EdgeValue", -2, 10)]
    [InlineData("EdgeValue", 2, 55)]
    [InlineData("LoopWithInnerConditionalBreak", 2049, 2049)]
    [InlineData("LoopWithInnerConditionalBreak", 2047, 2052)]
    public void ScalarCilMatchesCpuAndEmittedControl(string fixture, int input, int golden)
    {
        var reference = ScalarFixture(fixture);
        var expected = reference(input);
        Assert.Equal(golden, expected);
        Check(reference.Method, [new Value.Integer(input)], new Value.Integer(expected));
    }

    [Theory]
    [InlineData("shared-tail")]
    [InlineData("multiple-return")]
    [InlineData("zero-loop")]
    [InlineData("ordinary-loop")]
    [InlineData("continue-break")]
    [InlineData("nested-loop")]
    [InlineData("inner-early-return")]
    public void RepresentativeScalarCilCaptures(string scenario)
    {
        var (method, arguments, expected) = scenario switch
        {
            "shared-tail" => Case(
                (Func<int, int>)ScalarControlFlowFixtures.SharedTail,
                [new Value.Integer(2)],
                58),
            "multiple-return" => Case(
                (Func<int, int>)ScalarControlFlowFixtures.MultipleReturns,
                [new Value.Integer(-2)],
                -9),
            "zero-loop" => Case(
                (Func<int, int>)DevelopTestShaderModule.MinimumLoop,
                [new Value.Integer(0)],
                0),
            "ordinary-loop" => Case(
                (Func<int, int>)DevelopTestShaderModule.MinimumLoop,
                [new Value.Integer(4)],
                4),
            "continue-break" => Case(
                (Func<int, int>)ScalarControlFlowFixtures.ContinueAndBreak,
                [new Value.Integer(8)],
                120),
            "nested-loop" => Case(
                (Func<int, int, int, int, int>)DevelopTestShaderModule.NestedLoop,
                [
                    new Value.Integer(5),
                    new Value.Integer(7),
                    new Value.Integer(2),
                    new Value.Integer(3)
                ],
                52),
            "inner-early-return" => Case(
                (Func<int, int, int, int>)ScalarControlFlowFixtures.NestedEarlyReturn,
                [new Value.Integer(2), new Value.Integer(3), new Value.Integer(1)],
                1003),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        output.WriteLine($"ACTUAL capture scenario={scenario}");
        Check(method, arguments, expected);
    }

    [Theory]
    [InlineData(false, 29)]
    [InlineData(true, 11)]
    public void ConditionalReturnMatchesCpuAndEmittedControl(bool choose, int golden)
    {
        Func<int, bool, int, int> reference = ScalarControlFlowFixtures.ConditionalReturn;
        Assert.Equal(golden, reference(11, choose, 29));
        Check(reference.Method,
            [new Value.Integer(11), new Value.Boolean(choose), new Value.Integer(29)],
            new Value.Integer(golden));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BooleanParametersAndReturnsMatchCpuAndEmittedControl(bool input)
    {
        Func<bool, bool> reference = ScalarControlFlowFixtures.BooleanBranch;
        var expected = reference(input);
        Assert.Equal(!input, expected);
        var source = Check(reference.Method, [new Value.Boolean(input)], new Value.Boolean(expected));
        await new SlangService().ValidateAsync(source);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 3, 0)]
    [InlineData(1, 0, 5)]
    [InlineData(1, 1, 12)]
    [InlineData(1, 3, 26)]
    [InlineData(2, 0, 10)]
    [InlineData(2, 3, 52)]
    public void NestedLoopsMatchCpuAndEmittedControl(int outer, int inner, int golden)
    {
        Func<int, int, int, int, int> reference = DevelopTestShaderModule.NestedLoop;
        var expected = reference(5, 7, outer, inner);
        Assert.Equal(golden, expected);
        Check(reference.Method,
            [new Value.Integer(5), new Value.Integer(7), new Value.Integer(outer), new Value.Integer(inner)],
            new Value.Integer(expected));
    }

    [Theory]
    [InlineData(0, 6, 18)]
    [InlineData(1, 0, 22)]
    [InlineData(2, 0, 43)]
    [InlineData(1, 3, 72)]
    [InlineData(2, 6, 24428)]
    public void NestedBreakAndContinueMatchCpuAndEmittedControl(int outer, int inner, int golden)
    {
        Func<int, int, int> reference = ScalarControlFlowFixtures.NestedLoopControl;
        Assert.Equal(golden, reference(outer, inner));
        Check(reference.Method, [new Value.Integer(outer), new Value.Integer(inner)], new Value.Integer(golden));
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(1, 23)]
    [InlineData(2, 57)]
    [InlineData(3, 158)]
    public void ThreeNestedLoopsMatchCpuAndEmittedControl(int count, int golden)
    {
        Func<int, int> reference = ScalarControlFlowFixtures.ThreeNestedLoops;
        Assert.Equal(golden, reference(count));
        Check(reference.Method, [new Value.Integer(count)], new Value.Integer(golden));
    }

    [Theory]
    [InlineData(0, 3, 1, 18)]
    [InlineData(2, 0, 1, 43)]
    [InlineData(2, 3, 3, 21708)]
    [InlineData(2, 3, 0, 1001)]
    [InlineData(2, 3, 1, 1003)]
    public void NestedEarlyReturnMatchesCpuAndEmittedControl(int outer, int inner, int stop, int golden)
    {
        Func<int, int, int, int> reference = ScalarControlFlowFixtures.NestedEarlyReturn;
        Assert.Equal(golden, reference(outer, inner, stop));
        Check(reference.Method,
            [new Value.Integer(outer), new Value.Integer(inner), new Value.Integer(stop)], new Value.Integer(golden));
    }

    private string Check(MethodInfo method, ImmutableArray<Value> arguments, Value expected)
    {
        var stages = CompilerTestPipeline.CompileStages(method);
        var original = Assert.Single(
            stages.Compiled.FunctionDefinitions.Values,
            body => body.Declaration.Name == method.Name);
        var model = Assert.Single(
            stages.Labelled.FunctionDefinitions.Values,
            body => body.Environment.Method == method);
        Assert.True(model.Labels.ToHashSet().SetEquals(original.Labels));
        foreach (var label in model.Labels)
        {
            var range = model[label];
            output.WriteLine($"{method.Name} {label}: IL_{range.ByteOffset:X4}, {range.InstructionCount} instructions, " +
                $"successors {string.Join(", ", range.Terminator.ToSuccessor().AllTargets())}");
        }
        var valueControlFlow = Assert.Single(
            stages.ValueControlFlow.FunctionDefinitions.Values,
            body => body.Source.Source.Source.Environment.Method == method);
        output.WriteLine("ACTUAL input value CFG:");
        output.WriteLine(valueControlFlow.Graph.PrettyPrint());
        output.WriteLine("ACTUAL output region/control:");
        output.WriteLine(original.Dump());
        var lowered = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(original));
        var target = Lower(lowered);
        var source = Emit(target);
        output.WriteLine(source);
        var cfg = RunCfg(original, arguments);
        var scoped = RunScoped(original, arguments);
        Assert.Equal(expected, cfg.Result);
        AssertEquivalent(cfg, scoped);
        AssertEquivalent(cfg, RunCfg(lowered, arguments));
        var emitted = new EmittedScalarProgram(lowered, source).Run(arguments);
        output.WriteLine(
            $"ACTUAL original CFG result={cfg.Result} trace={string.Join(" -> ", cfg.Trace)}");
        output.WriteLine(
            $"ACTUAL scoped result={scoped.Result} trace={string.Join(" -> ", scoped.Trace)}");
        output.WriteLine($"ACTUAL emitted result={emitted.Result} trace={string.Join(" -> ", emitted.Trace)}");
        AssertEquivalent(cfg, emitted);
        return source;
    }

    private static (MethodInfo Method, ImmutableArray<Value> Arguments, Value Expected) Case(
        Delegate reference,
        ImmutableArray<Value> arguments,
        int expected) =>
        (reference.Method, arguments, new Value.Integer(expected));

    private static Func<int, int> ScalarFixture(string fixture) =>
        fixture switch
        {
            "SharedTail" => ScalarControlFlowFixtures.SharedTail,
            "MultipleReturns" => ScalarControlFlowFixtures.MultipleReturns,
            "MinimumLoop" => DevelopTestShaderModule.MinimumLoop,
            "ContinueAndBreak" => ScalarControlFlowFixtures.ContinueAndBreak,
            "LoopCarriedSwap" => ScalarControlFlowFixtures.LoopCarriedSwap,
            "EdgeValue" => ScalarControlFlowFixtures.EdgeValue,
            "LoopWithInnerConditionalBreak" => DevelopTestShaderModule.LoopWithInnerConditionalBreak,
            _ => throw new ArgumentOutOfRangeException(nameof(fixture))
        };

    internal static string Emit(FunctionBody4 body) => Emit(Lower(body));

    internal static string Emit(SlangFunctionBody body) =>
        new SlangEmitter(new ShaderModuleDeclaration<SlangFunctionBody>(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, SlangFunctionBody>.Empty.Add(body.Declaration, body)))
        .Emit();

    internal static SlangFunctionBody Lower(FunctionBody4 body) =>
        new SlangTargetLowering().Lower(new ShaderModuleDeclaration<FunctionBody4>(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty.Add(body.Declaration, body)))
        .GetBody(body.Declaration);

    [Fact]
    public void FixtureHasActualConfigurationSpecificCil()
    {
        var assembly = typeof(ScalarControlFlowFixtures).Assembly;
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
#if DEBUG
        Assert.Equal("Debug", configuration);
#else
        Assert.Equal("Release", configuration);
#endif
        var method = ((Func<int, int>)ScalarControlFlowFixtures.MultipleReturns).Method;
        var model = CompilerTestPipeline.Labelled(method);
        var returns = model.Labels.Where(label => model[label].Terminator.ToSuccessor() is TerminateSuccessor)
            .ToArray();
        var debuggable = assembly.GetCustomAttribute<DebuggableAttribute>() ??
            throw new InvalidOperationException("Missing compiler configuration metadata.");
        switch (configuration)
        {
            case "Debug":
                Assert.True(debuggable.IsJITOptimizerDisabled);
                Assert.Single(returns);
                Assert.Equal(3, model.Blocks.Blocks.Count(
                    block => block.Terminator.ToSuccessor().AllTargets().Contains(returns[0])));
                break;
            case "Release":
                Assert.False(debuggable.IsJITOptimizerDisabled);
                Assert.Equal(3, returns.Length);
                break;
            default:
                throw new InvalidOperationException($"Unsupported build configuration {configuration}");
        }
        output.WriteLine($"{configuration}: {model.CodeByteSize} CIL bytes, {model.Labels.Length} blocks, " +
            $"{returns.Length} terminal return blocks.");

        var edge = CompilerTestPipeline.CompileBody(((Func<int, int>)ScalarControlFlowFixtures.EdgeValue).Method);
        Assert.Contains(edge.Labels, label => !edge[label].Parameters.IsEmpty);
    }

    [Fact]
    public void SharedTailAndNestedLoopFixturesHaveTheRequiredTopology()
    {
        var body = CompilerTestPipeline.CompileBody(((Func<int, int>)ScalarControlFlowFixtures.SharedTail).Method);
        var branch = Assert.Single(body.Labels,
            label => body[label].Body.Last is Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>);
        var tail = Assert.IsType<ExitPostDominance.Block>(body[branch].PostDominance).Target;
        Assert.Contains(body[tail].Body.Elements,
            instruction => instruction.Operation is IBinaryExpressionOperation { BinaryOp: BinaryArithmetic.Mul });

        var nestedMethod = ((Func<int, int, int, int, int>)DevelopTestShaderModule.NestedLoop).Method;
        var nested = CompilerTestPipeline.ControlFacts(nestedMethod);
        Assert.Equal(2, nested.Graph.Labels().Count(
            label => nested.Graph[label].Annotation.IsLoopHeader));
    }

    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    internal static FunctionBody4 HandBody()
    {
        var entry = Label.Create("entry");
        var tail = Label.Create("tail");
        var exit = Label.Create("exit");
        var input = new ParameterDeclaration("x", ShaderType.I32, []);
        var declaration = new FunctionDeclaration("Probe", [input], new FunctionReturn(ShaderType.I32, []), []);
        var local = new VariableDeclaration(FunctionAddressSpace.Instance, "r", ShaderType.I32, []);
        var argument = ShaderValue.Intermediate(ShaderType.I32);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var incoming = ShaderValue.Intermediate(ShaderType.I32);
        var carried = ShaderValue.Intermediate(ShaderType.I32);
        var before = ShaderValue.Intermediate(ShaderType.I32);
        var incremented = ShaderValue.Intermediate(ShaderType.I32);
        var after = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var blocks = new[]
        {
            Body(entry, [], [
                Instruction.Factory.Store(default, new StoreOperation(), local.Value, Int(0)),
                Instruction.Factory.Load(default, new LoadOperation(), argument, input.Value),
                Instruction.Factory.Operation2(default, NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>.Instance,
                    condition, argument, Int(0))
            ], Terms.BrIf(condition, new(tail, [Int(10)]), new(tail, [Int(20)]))),
            Body(tail, [incoming], [
                Instruction.Factory.Load(default, new LoadOperation(), before, local.Value),
                Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    incremented, before, Int(1)),
                Instruction.Factory.Store(default, new StoreOperation(), local.Value, incremented)
            ], Terms.Br(new(exit, [incoming]))),
            Body(exit, [carried], [
                Instruction.Factory.Load(default, new LoadOperation(), after, local.Value),
                Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result, after, carried)
            ], Terms.ReturnExpr(result))
        };
        return CreateFunctionBody(declaration,
            RegionTree.Block(entry, [
                RegionTree.Block(exit, [], blocks[2], null),
                RegionTree.Block(tail, [], blocks[1], exit)
            ], blocks[0], tail));
    }

    private static IShaderValue Int(int value) => ShaderValue.Literal(new I32Literal(value));

    private const string ProbeTail = """
        {
        // block ^1Label(tail)
        r = r + 1;
        }
        """;

    private static string ProbeSource(string tail, string edgeValue) => $$"""
        i32 Probe(i32 x, )
        {
        var r : i32;
        var edge : i32;
        // block ^0Label(entry)
        r = 0;
        if(x > 0)
        {
        edge = {{edgeValue}};
        }
        else
        {
        edge = 20;
        }
        {{tail}}
        // block ^2Label(exit)
        r = r + edge;
        return r;
        }
        """;

    [Fact]
    public void OracleNegativeControlsDetectDuplicateSkipAndWrongSelectedEdge()
    {
        var body = HandBody();
        var expected = new Execution(new Value.Integer(11), body.Labels);
        AssertEquivalent(expected, RunCfg(body, [new Value.Integer(1)]));
        Execution Run(string tail, string edge) =>
            new EmittedScalarProgram(body, ProbeSource(tail, edge)).Run([new Value.Integer(1)]);
        AssertEquivalent(expected, Run(ProbeTail, "10"));
        var falseEdge = RunCfg(body, [new Value.Integer(-1)]);
        Assert.Equal(new Value.Integer(21), falseEdge.Result);
        AssertEquivalent(falseEdge,
            new EmittedScalarProgram(body, ProbeSource(ProbeTail, "10")).Run([new Value.Integer(-1)]));
        var repeated = Run(ProbeTail + "\n" + ProbeTail, "10");
        var skipped = Run("", "10");
        var wrongEdge = Run(ProbeTail, "20");
        Assert.Equal(new Value.Integer(12), repeated.Result);
        Assert.Equal(new Value.Integer(10), skipped.Result);
        Assert.Equal(new Value.Integer(21), wrongEdge.Result);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertEquivalent(expected, repeated));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertEquivalent(expected, skipped));
        Assert.True(expected.Trace.SequenceEqual(wrongEdge.Trace));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertEquivalent(expected, wrongEdge));
    }

    [Fact]
    public void CfgEdgeArgumentsAreParallelCopiesAndLegitimateRevisitsRemainInTrace()
    {
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var exit = Label.Create("exit");
        var a = ShaderValue.Intermediate(ShaderType.I32);
        var b = ShaderValue.Intermediate(ShaderType.I32);
        var again = ShaderValue.Intermediate(ShaderType.Bool);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var tens = ShaderValue.Intermediate(ShaderType.I32);
        var answer = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration("Swap", [], new FunctionReturn(ShaderType.I32, []), []);
        var entryBody = Body(entry, [], [],
            Terms.Br(new(loop, [Int(1), Int(2), ShaderValue.Literal(new BoolLiteral(true))])));
        var loopBody = Body(loop, [a, b, again], [
            Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Mul>.Instance,
                tens, a, Int(10)),
            Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                result, tens, b)
        ], Terms.BrIf(again, new(loop, [b, a, ShaderValue.Literal(new BoolLiteral(false))]),
            new(exit, [result])));
        var exitBody = Body(
            exit,
            [answer],
            [],
            Terms.ReturnExpr(answer));
        var body = CreateFunctionBody(declaration, RegionTree.Block(entry, [
            RegionTree.Block(exit, [], exitBody, null),
            RegionTree.Loop(loop, [], loopBody, exit, exit)
        ], entryBody, loop));
        var expected = new Execution(new Value.Integer(21), [entry, loop, loop, exit]);
        AssertEquivalent(expected, RunCfg(body, []));
        AssertEquivalent(expected, RunScoped(body, []));
        var lowered = new RegionParameterToLocalVariablePass().VisitFunctionBody(body);
        AssertEquivalent(expected, RunCfg(lowered, []));
        AssertEquivalent(expected, RunScoped(lowered, []));
    }

    [Fact]
    public void UnsupportedOperationsAndBothExecutionBudgetsFailExplicitly()
    {
        var body = HandBody();
        var cycleLabel = Label.Create("cycle");
        var cycleRegion = Body(
            cycleLabel,
            [],
            [],
            Terms.Br(new(cycleLabel, [])));
        var cycle = CreateFunctionBody(
            new FunctionDeclaration("Cycle", [], new FunctionReturn(ShaderType.I32, []), []),
            RegionTree.Loop(cycleLabel, [], cycleRegion, null, null));
        Assert.Single(cycle.Labels);
        Assert.Contains("step budget", Assert.Throws<InvalidOperationException>(
            () => RunCfg(cycle, [], 100)).Message);
        Assert.Contains("step budget", Assert.Throws<InvalidOperationException>(
            () => RunScoped(cycle, [], 100)).Message);
        var infinite = "i32 Probe(i32 x, )\n{\nwhile(true)\n{\n}\n}";
        Assert.Contains("step budget", Assert.Throws<InvalidOperationException>(
            () => new EmittedScalarProgram(body, infinite).Run([new Value.Integer(0)], 10)).Message);
        Assert.Throws<NotSupportedException>(() =>
            new EmittedScalarProgram(body, ProbeSource(ProbeTail, "10 / 2")));
        Assert.Throws<NotSupportedException>(() =>
            new EmittedScalarProgram(body, ProbeSource(ProbeTail, "unknown()")));
        Assert.Throws<InvalidOperationException>(() =>
            new EmittedScalarProgram(body, "i32 Probe(i32 x, )\n{\nvar r : i32;\nreturn r;\n}")
                .Run([new Value.Integer(0)]));
        var unsupported = body.MapRegionBody(block => block.Label != body.Entry ? block : block with
        {
            Body = Seq.Create([
                Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Div>.Instance,
                    ShaderValue.Intermediate(ShaderType.I32), Int(4), Int(2))
            ], block.Body.Last)
        });
        Assert.Contains("Unsupported scalar binary operation", Assert.Throws<NotSupportedException>(
            () => RunCfg(unsupported, [new Value.Integer(0)])).Message);
        Assert.Throws<NotSupportedException>(() =>
            new EmittedScalarProgram(body, ProbeSource(ProbeTail.Replace("Label(tail)", "Label(unknown)",
                StringComparison.Ordinal), "10")));
    }

    [Fact]
    public void ScalarFixturesCompileThroughPublicSlangAndWgslApis()
    {
        var shader = new ScalarControlFlowShader();
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        Assert.Contains("SharedTail", slang);
        Assert.Contains("ContinueAndBreak", slang);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("fn ScalarFragment", wgsl);
    }

    [Fact]
    public void ScalarBooleanCallCompilesThroughPublicWgslApi()
    {
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(new ScalarBooleanCallShader());
        Assert.Contains("fn BooleanFragment", wgsl);
    }
}
