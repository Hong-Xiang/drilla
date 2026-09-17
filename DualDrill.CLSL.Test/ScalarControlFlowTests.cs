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
using DualDrill.Common.Nat;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

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
        Func<int, int> reference = fixture switch
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
        var expected = reference(input);
        Assert.Equal(golden, expected);
        Check(reference.Method, [new Value.Integer(input)], new Value.Integer(expected));
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
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(method);
        var original = parser.MethodBodies[declaration];
        var model = parser.Context.GetFunctionDefinition(declaration);
        Assert.True(model.Labels.ToHashSet().SetEquals(original.Labels));
        foreach (var label in model.Labels)
        {
            var range = model.ControlFlowGraph[label];
            output.WriteLine($"{method.Name} {label}: IL_{range.ByteOffset:X4}, {range.InstructionCount} instructions, " +
                $"successors {string.Join(", ", model.ControlFlowGraph.GetSucc(label))}");
        }
        var lowered = new RegionParameterToLocalVariablePass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(original));
        var source = Emit(lowered);
        output.WriteLine(source);
        var cfg = RunCfg(original, arguments);
        Assert.Equal(expected, cfg.Result);
        AssertEquivalent(cfg, RunCfg(lowered, arguments));
        var emitted = new EmittedScalarProgram(lowered, source).Run(arguments);
        output.WriteLine($"CPU/original CFG: {cfg.Result}; emitted: {emitted.Result}");
        AssertEquivalent(cfg, emitted);
        return source;
    }

    internal static string Emit(FunctionBody4 body) =>
        new SlangEmitter(new ShaderModuleDeclaration<FunctionBody4>(
            [body.Declaration], ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty.Add(body.Declaration, body)))
        .Emit();

    private static void AssertEquivalent(Execution expected, Execution actual)
    {
        Assert.True(expected.Trace.SequenceEqual(actual.Trace),
            $"Expected result: {expected.Result}; actual: {actual.Result}\n" +
            $"Original blocks: {string.Join(" -> ", expected.Trace)}\nExecuted blocks: {string.Join(" -> ", actual.Trace)}");
        Assert.Equal(expected.Result, actual.Result);
    }

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
        var model = new MethodBodyAnalysisModel(method);
        var returns = model.Labels.Where(label => model.ControlFlowGraph.Successor(label) is TerminateSuccessor)
            .ToArray();
        var debuggable = assembly.GetCustomAttribute<DebuggableAttribute>() ??
            throw new InvalidOperationException("Missing compiler configuration metadata.");
        switch (configuration)
        {
            case "Debug":
                Assert.True(debuggable.IsJITOptimizerDisabled);
                Assert.Single(returns);
                Assert.Equal(3, model.ControlFlowGraph.GetPred(returns[0]).Count());
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

        var parser = new RuntimeReflectionParser();
        var edge = parser.ParseMethod(((Func<int, int>)ScalarControlFlowFixtures.EdgeValue).Method);
        Assert.Contains(parser.MethodBodies[edge].Labels, label => !parser.MethodBodies[edge][label].Parameters.IsEmpty);
    }

    [Fact]
    public void SharedTailAndNestedLoopFixturesHaveTheRequiredTopology()
    {
        var parser = new RuntimeReflectionParser();
        var shared = parser.ParseMethod(((Func<int, int>)ScalarControlFlowFixtures.SharedTail).Method);
        var body = parser.MethodBodies[shared];
        var branch = Assert.Single(body.Labels,
            label => body[label].Body.Last is Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>);
        var tail = body[branch].ImmediatePostDominator;
        Assert.NotNull(tail);
        Assert.Contains(body[tail].Body.Elements,
            instruction => instruction.Operation is IBinaryExpressionOperation { BinaryOp: BinaryArithmetic.Mul });

        var nested = new MethodBodyAnalysisModel(((Func<int, int, int, int, int>)DevelopTestShaderModule.NestedLoop).Method);
        var analysis = nested.ControlFlowGraph.ControlFlowAnalysis();
        Assert.Equal(2, nested.Labels.Count(analysis.IsLoop));
    }

    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    private static FunctionBody4 HandBody()
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
            ShaderRegionBody.Create(entry, [], [
                Instruction.Factory.Store(default, new StoreOperation(), local.Value, Int(0)),
                Instruction.Factory.Load(default, new LoadOperation(), argument, input.Value),
                Instruction.Factory.Operation2(default, NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>.Instance,
                    condition, argument, Int(0))
            ], Terms.BrIf(condition, new(tail, [Int(10)]), new(tail, [Int(20)])), tail),
            ShaderRegionBody.Create(tail, [incoming], [
                Instruction.Factory.Load(default, new LoadOperation(), before, local.Value),
                Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    incremented, before, Int(1)),
                Instruction.Factory.Store(default, new StoreOperation(), local.Value, incremented)
            ], Terms.Br(new(exit, [incoming])), exit),
            ShaderRegionBody.Create(exit, [carried], [
                Instruction.Factory.Load(default, new LoadOperation(), after, local.Value),
                Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result, after, carried)
            ], Terms.ReturnExpr(result), null)
        };
        return new FunctionBody4(declaration,
            RegionTree<Label, ShaderRegionBody>.Block(entry, [
                RegionTree<Label, ShaderRegionBody>.Block(tail, [], blocks[1], exit),
                RegionTree<Label, ShaderRegionBody>.Block(exit, [], blocks[2], null)
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
        var entryBody = ShaderRegionBody.Create(entry, [], [],
            Terms.Br(new(loop, [Int(1), Int(2), ShaderValue.Literal(new BoolLiteral(true))])), loop);
        var loopBody = ShaderRegionBody.Create(loop, [a, b, again], [
            Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Mul>.Instance,
                tens, a, Int(10)),
            Instruction.Factory.Operation2(default, NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                result, tens, b)
        ], Terms.BrIf(again, new(loop, [b, a, ShaderValue.Literal(new BoolLiteral(false))]),
            new(exit, [result])), exit);
        var exitBody = ShaderRegionBody.Create(exit, [answer], [], Terms.ReturnExpr(answer), null);
        var body = new FunctionBody4(declaration, RegionTree<Label, ShaderRegionBody>.Block(entry, [
            RegionTree<Label, ShaderRegionBody>.Loop(loop, [], loopBody, exit, exit),
            RegionTree<Label, ShaderRegionBody>.Block(exit, [], exitBody, null)
        ], entryBody, loop));
        var expected = new Execution(new Value.Integer(21), [entry, loop, loop, exit]);
        AssertEquivalent(expected, RunCfg(body, []));
        var lowered = new RegionParameterToLocalVariablePass().VisitFunctionBody(body);
        AssertEquivalent(expected, RunCfg(lowered, []));
    }

    [Fact]
    public void UnsupportedOperationsAndBothExecutionBudgetsFailExplicitly()
    {
        var body = HandBody();
        var cycle = body.MapRegionBody(block => block.Label != body.Entry ? block : block with
        {
            Body = Seq.Create(block.Body.Elements, Terms.Br(new(body.Entry, [])))
        });
        Assert.Contains("step budget", Assert.Throws<InvalidOperationException>(
            () => RunCfg(cycle, [new Value.Integer(0)], 100)).Message);
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
