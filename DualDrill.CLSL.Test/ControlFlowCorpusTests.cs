using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.Common.CodeTextWriter;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ControlFlowCorpus;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class ControlFlowCorpusTests(ITestOutputHelper output)
{
    [Fact]
    public void ExhaustiveForwardGraphsReachAllOrderedArms()
    {
        var cases = ExhaustiveForward();
        Assert.Equal([1, 2, 10, 80], cases.GroupBy(@case => @case.NodeCount)
            .OrderBy(group => group.Key)
            .Select(group => group.Count()));
        Assert.Equal(93, cases.Length);
        Assert.Equal(cases.Length, cases.Select(@case => @case.Id).Distinct().Count());

        var failures = VerifyForwardCases(cases);

        output.WriteLine($"unique exhaustive cases ({cases.Length}): {string.Join(", ", cases.Select(c => c.Id))}");
        AssertNoFailures(failures);
    }

    [Fact]
    public void SeededLargerForwardGraphsReachAllOrderedArms()
    {
        var cases = SampledForward();
        Assert.Equal(SampleCount, cases.Length);
        Assert.Equal(cases.Length, cases.Select(@case => @case.Encoding).Distinct().Count());
        Assert.Equal(5, cases.Min(@case => @case.NodeCount));
        Assert.Equal(12, cases.Max(@case => @case.NodeCount));

        var failures = VerifyForwardCases(cases);

        output.WriteLine(
            $"seed={SampleSeed} unique samples={cases.Length} " +
            $"nodes={cases.Min(c => c.NodeCount)}..{cases.Max(c => c.NodeCount)} " +
            $"edges={cases.Min(c => c.EdgeCount)}..{cases.Max(c => c.EdgeCount)}");
        output.WriteLine($"sample encodings: {string.Join(" | ", cases.Select(c => $"{c.Id}={c.Encoding}"))}");
        AssertNoFailures(failures);
    }

    [Fact]
    public void NamedCrossingAndStaggeredJoinTopologiesRemainInTheExhaustiveCorpus()
    {
        var cases = ExhaustiveForward().ToDictionary(@case => @case.Id);
        Assert.Equal("1,2;2,3;3;R", cases["forward-4-0118"].Encoding);
        Assert.Equal("1,3;2,3;3;R", cases["forward-4-0139"].Encoding);
    }

    [Fact]
    public async Task RepresentativeGeneratedAndHandSourcesPassNativeSlangValidation()
    {
        var crossing = ExhaustiveForward().Single(@case => @case.Id == "forward-4-0118");
        foreach (var raw in new[] { Forward(crossing), MultipleLatchesAndExits() })
        {
            var capture = new Capture($"native-validation/{raw.Id}");
            capture.Write("input.txt", raw.Input);
            var compiled = Compile(raw, capture);
            await ValidateNative(capture, "native-slang-validation.txt", compiled.Source);
        }
    }

    [Fact]
    public void LabelRenamingAndDefinitionReorderingPreserveIdentitySemantics()
    {
        var spec = SampledForward()[17];
        var variants = new[]
        {
            Forward(spec),
            Forward(spec, "renamed_"),
            Forward(spec, reverseStorage: true)
        };
        var witnesses = Witnesses(spec);
        ImmutableArray<ReferenceExecution>? baseline = null;

        foreach (var raw in variants)
        {
            var capture = new Capture($"metamorphic/{raw.Id}");
            capture.Write("input.txt", raw.Input);
            var references = witnesses.Select(witness => Reference(spec, witness.Arguments)).ToImmutableArray();
            var execution = new List<string>();
            var flat = witnesses.Select(witness => RunRaw(raw, witness.Arguments)).ToImmutableArray();
            foreach (var ((witness, reference), actual) in witnesses.Zip(references).Zip(flat))
            {
                AssertExecution(reference, actual, raw.Identity);
            }
            capture.Write(
                "raw-execution.txt",
                string.Join(
                    Environment.NewLine,
                    witnesses.Zip(flat).Select(pair => FormatExecution(pair.First, pair.Second, raw.Identity))));
            var compiled = Compile(raw, capture);
            foreach (var ((witness, reference), actual) in witnesses.Zip(references).Zip(flat))
            {
                AssertPipeline(compiled, witness.Arguments, reference);
                execution.Add(
                    $"{FormatExecution(witness, actual, raw.Identity)}{Environment.NewLine}" +
                    FormatPipeline(compiled, witness.Arguments));
            }
            capture.Write("execution.txt", string.Join(Environment.NewLine, execution));

            if (baseline is null)
                baseline = references;
            else
                foreach (var (expected, actual) in baseline.Value.Zip(references))
                {
                    Assert.Equal(expected.Result, actual.Result);
                    Assert.True(expected.Trace.SequenceEqual(actual.Trace));
                    Assert.True(expected.Arms.SequenceEqual(actual.Arms));
                }
        }
    }

    [Theory]
    [InlineData(false, 29)]
    [InlineData(true, 11)]
    public void SameTargetOrderedArmsRetainSelectedValues(bool choose, int expected)
    {
        var raw = SameTarget();
        var arguments = ImmutableArray.Create<Value>(new Value.Boolean(choose));
        var capture = new Capture(raw.Id);
        capture.Write("input.txt", raw.Input);
        var flat = RunRaw(raw, arguments);
        Assert.Equal(new Value.Integer(expected), flat.Result);
        capture.Write($"raw-execution-{choose}.txt", FormatFlat(arguments, flat, raw.Identity));
        var compiled = Compile(raw, capture);
        var exit = compiled.Facts.Labels().Single(label => label.Name == "same-exit");
        var arms = compiled.Facts[exit].Annotation.IncomingArms;
        Assert.Equal([0, 1], arms.Select(arm => arm.SuccessorIndex));
        Assert.All(arms, arm => Assert.Equal("same-entry", arm.Source.Name));
        AssertPipeline(compiled, arguments, flat);
        capture.Write($"execution-{choose}.txt", FormatPipeline(compiled, arguments));
    }

    [Theory]
    [InlineData(0, 13)]
    [InlineData(1, 31)]
    [InlineData(4, 13)]
    public void SelectedBackedgeSwapsInParallel(int iterations, int expected)
    {
        var raw = ParallelSwap();
        var arguments = ImmutableArray.Create<Value>(new Value.Integer(iterations));
        var capture = new Capture(raw.Id);
        capture.Write("input.txt", raw.Input);
        var flat = RunRaw(raw, arguments);
        Assert.Equal(new Value.Integer(expected), flat.Result);
        Assert.Equal(iterations + 2, flat.Trace.Length);
        capture.Write($"raw-execution-{iterations}.txt", FormatFlat(arguments, flat, raw.Identity));
        var compiled = Compile(raw, capture);
        var loop = compiled.Facts.Labels().Single(label => label.Name == "swap-loop");
        Assert.Contains(
            compiled.Facts[loop].Annotation.IncomingArms,
            arm => arm.IsBackedge && ReferenceEquals(arm.Source, loop));
        AssertPipeline(compiled, arguments, flat);
        capture.Write($"execution-{iterations}.txt", FormatPipeline(compiled, arguments));
    }

    [Fact]
    public void NaturalLoopHasTwoLatchesAndTwoObservableExits()
    {
        var raw = MultipleLatchesAndExits();
        var firstArguments = ImmutableArray.Create<Value>(new Value.Boolean(true), new Value.Integer(3));
        var secondArguments = ImmutableArray.Create<Value>(new Value.Boolean(false), new Value.Integer(3));
        var capture = new Capture(raw.Id);
        capture.Write("input.txt", raw.Input);
        var first = RunRaw(raw, firstArguments);
        var second = RunRaw(raw, secondArguments);
        Assert.Equal(new Value.Integer(13), first.Result);
        Assert.Equal(new Value.Integer(23), second.Result);
        Assert.Equal(11, first.Trace.Length);
        Assert.Equal(13, second.Trace.Length);
        capture.Write(
            "raw-execution.txt",
            FormatFlat(firstArguments, first, raw.Identity) + Environment.NewLine +
            FormatFlat(secondArguments, second, raw.Identity));
        var compiled = Compile(raw, capture);
        var header = compiled.Facts.Labels().Single(label => label.Name == "multi-header");
        var backedges = compiled.Facts[header].Annotation.IncomingArms
            .Where(arm => arm.IsBackedge)
            .Select(arm => arm.Source.Name!)
            .Order()
            .ToArray();
        Assert.Equal(["multi-left-latch", "multi-right-latch"], backedges);
        var loopMembers = NaturalLoopMembers(compiled.Facts, header);
        var exitSources = loopMembers
            .Where(source => compiled.Facts.Successor(source).AllTargets()
                .Any(target => !loopMembers.Contains(target)))
            .Select(source => source.Name!)
            .Order()
            .ToArray();
        Assert.Equal(["multi-header", "multi-left-latch"], exitSources);
        Assert.Contains(first.Trace, label => label.Name == "multi-first-exit");
        Assert.Contains(second.Trace, label => label.Name == "multi-second-exit");
        AssertPipeline(compiled, firstArguments, first);
        AssertPipeline(compiled, secondArguments, second);
        capture.Write(
            "execution.txt",
            FormatPipeline(compiled, firstArguments) + Environment.NewLine +
            FormatPipeline(compiled, secondArguments));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void OneTripLoopChainsHaveActualComputedDepth(int depth)
    {
        var raw = NestedOneTripLoops(depth);
        var stepLimit = depth * 64 + 64;
        var capture = new Capture(raw.Id, stepLimit);
        capture.Write("input.txt", raw.Input);
        var flat = RunRaw(raw, [], stepLimit);
        Assert.Equal(new Value.Integer(1), flat.Result);
        capture.Write("raw-execution.txt", FormatFlat([], flat, raw.Identity));
        var compiled = Compile(raw, capture);
        var measured = MaximumLoopDepth(compiled.Region.Body);
        Assert.Equal(depth, measured.Depth);
        Assert.Equal(depth, measured.Headers.Distinct().Count());
        Assert.All(measured.Headers, header => Assert.Contains("header", header.Name));
        AssertPipeline(compiled, [], flat, stepLimit);
        capture.Write("execution.txt", FormatPipeline(compiled, [], stepLimit));
    }

    [Fact]
    public void IrreducibleTwoEntryAndSideEntryCyclesAreRejectedStructurally()
    {
        foreach (var raw in IrreducibleControls())
        {
            var capture = new Capture(raw.Id);
            capture.Write("input.txt", raw.Input);
            var facts = Annotate(raw);
            capture.Write("facts.txt", facts.PrettyPrint());
            var error = Assert.Throws<ArgumentException>(() =>
                DualDrill.CLSL.Language.Region.RegionTree.Create(
                    facts,
                    static (label, block, control) => new ShaderRegionBody(
                        label,
                        block.Parameters,
                        block.Body,
                        control.PostDominance)));
            capture.Write("diagnostic.txt", error.ToString());
            Assert.Contains("lexical scope", error.Message);
            Assert.DoesNotContain("budget", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task OrdinarySharpShaderUsesPublicParseCompileAndEmitBoundaries()
    {
        var shader = new CorpusOrdinaryShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var parsed = compiler.Parse(shader);
        var method = ((Func<int, int>)CorpusOrdinaryShader.OrdinaryControl).Method;
        var raw = CompilerTestPipeline.RawBody(parsed, method);
        var capture = new Capture("cil/ordinary-public");
        capture.Write("00-public-parse-cil.txt", raw.Code.PrettyPrint());
        var rawStage = CompilerTestPipeline.ParseRaw(method);
        capture.Write("01-raw-cil.txt", Assert.Single(rawStage.FunctionDefinitions.Values).Code.PrettyPrint());
        var preStage = CilPreStackPass.Run(rawStage);
        var pre = Assert.Single(preStage.FunctionDefinitions.Values);
        capture.Write("02-pre-cil.txt", pre.PrettyPrint());
        var labelledStage = CilBlockPartitionPass.Run(preStage);
        var labelled = Assert.Single(labelledStage.FunctionDefinitions.Values);
        capture.Write("03-labelled-cil.txt", labelled.PrettyPrint());
        var shaderStackStage = CilToShaderStackPass.Run(labelledStage);
        var shaderStack = Assert.Single(shaderStackStage.FunctionDefinitions.Values);
        capture.Write("04-shader-stack.txt", shaderStack.PrettyPrint());
        var shaderControlStage = ShaderStackControlFlowPass.Run(shaderStackStage);
        var shaderControl = Assert.Single(shaderControlStage.FunctionDefinitions.Values);
        capture.Write("05-shader-control-flow.txt", shaderControl.PrettyPrint());
        var valueStage = ShaderStackToValuePass.Run(shaderControlStage);
        var value = Assert.Single(valueStage.FunctionDefinitions.Values);
        capture.Write("06-value-cfg.txt", value.PrettyPrint());
        var promotedStage = CilLocalPromotionPass.Run(valueStage);
        var promoted = Assert.Single(promotedStage.FunctionDefinitions.Values);
        capture.Write("07-promoted-value-cfg.txt", promoted.PrettyPrint());
        var factsStage = CilBlockControlFactsPass.Run(promotedStage);
        var facts = Assert.Single(factsStage.FunctionDefinitions.Values);
        capture.Write("08-control-facts.txt", facts.PrettyPrint());
        var regionStage = CilRegionPass.Run(factsStage);
        var region = Assert.Single(regionStage.FunctionDefinitions.Values);
        capture.Write("09-region.txt", region.Dump());
        var normalized = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(region));
        var target = ScalarControlFlowTests.Lower(normalized);
        var scalarSource = ScalarControlFlowTests.Emit(target);
        capture.Write("10-normalized-region.txt", normalized.Dump());
        capture.Write("11-target-ast.txt", target.PrettyPrint());
        capture.Write("12-scalar-target.slang", scalarSource);

        var executions = new List<string>();
        foreach (var (input, golden) in new[] { (0, 17), (3, 21) })
        {
            Assert.Equal(golden, CorpusOrdinaryShader.OrdinaryControl(input));
            var arguments = ImmutableArray.Create<Value>(new Value.Integer(input));
            var valueExecution = RunValueCfg(value, arguments);
            var factsExecution = RunFactsCfg(facts, arguments);
            var regionExecution = RunCfg(region, arguments);
            var scopedExecution = ScopedContinuationOracle.RunScoped(region, arguments);
            var emittedExecution = new EmittedScalarProgram(normalized, scalarSource).Run(arguments);
            Assert.Equal(new Value.Integer(golden), valueExecution.Result);
            AssertEquivalent(valueExecution, factsExecution);
            AssertEquivalent(valueExecution, regionExecution);
            AssertEquivalent(valueExecution, scopedExecution);
            AssertEquivalent(valueExecution, emittedExecution);
            executions.Add(
                $"input={input} golden={golden}{Environment.NewLine}" +
                $"value result={valueExecution.Result} trace={string.Join(" -> ", valueExecution.Trace)}{Environment.NewLine}" +
                $"facts result={factsExecution.Result} trace={string.Join(" -> ", factsExecution.Trace)}{Environment.NewLine}" +
                $"region result={regionExecution.Result} trace={string.Join(" -> ", regionExecution.Trace)}{Environment.NewLine}" +
                $"scoped result={scopedExecution.Result} trace={string.Join(" -> ", scopedExecution.Trace)}{Environment.NewLine}" +
                $"emitted result={emittedExecution.Result} trace={string.Join(" -> ", emittedExecution.Trace)}");
        }
        capture.Write("13-execution.txt", string.Join(Environment.NewLine + Environment.NewLine, executions));

        var compiled = compiler.Compile(parsed);
        var source = compiler.Emit(shader);
        var publicRegion = Assert.Single(
            compiled.FunctionDefinitions.Values,
            body => body.Declaration.Name == method.Name);
        Assert.NotEmpty(raw.Code.Instructions);
        Assert.Contains("OrdinaryControl", source);
        capture.Write("14-public-region.txt", publicRegion.Dump());
        capture.Write("15-public-target.slang", source);
        await ValidateNative(capture, "16-public-slang-validation.txt", source);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Contains("fn Fragment", wgsl);
        capture.Write("17-public-target.wgsl", wgsl);
    }

    [Fact]
    public void DenseSwitchHasSwitchCilAndRetainsPublicFrontendRejection()
    {
        var shader = new CorpusDenseSwitchShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
        var parsed = compiler.Parse(shader);
        var method = ((Func<int, int>)CorpusDenseSwitchShader.DenseSwitch).Method;
        var raw = CompilerTestPipeline.RawBody(parsed, method);
        Assert.Contains(
            raw.Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Switch);
        var capture = new Capture("cil/dense-switch-rejection");
        capture.Write("cil.txt", raw.Code.PrettyPrint());

        var compileError = Assert.Throws<NotSupportedException>(() => compiler.Compile(parsed));
        capture.Write("compile-diagnostic.txt", compileError.ToString());
        Assert.Contains("CIL control Switch", compileError.Message);
        Assert.Contains("is not supported", compileError.Message);

        var emitError = Assert.Throws<NotSupportedException>(() => compiler.Emit(shader));
        capture.Write("emit-diagnostic.txt", emitError.ToString());
        Assert.Equal(compileError.Message, emitError.Message);
    }

    [Fact]
    public void DivergenceCorpusSeparatesSlangAndWgslCapabilities()
    {
        ISharpShader[] shaders = [new CorpusMixedDivergenceShader(), new CorpusNoExitShader()];
        foreach (var shader in shaders)
        {
            var slangCompiler = new CLSLCompiler(new(CLSLCompileTarget.SLang));
            var parsed = slangCompiler.Parse(shader);
            var capture = new Capture($"divergence/{shader.GetType().Name}", 100);
            capture.Write(
                "00-cil.txt",
                string.Join(
                    Environment.NewLine,
                    parsed.FunctionDefinitions.Values.Select(body => body.Code.PrettyPrint())));
            var compiled = slangCompiler.Compile(parsed);
            capture.Write(
                "01-region.txt",
                string.Join(
                    Environment.NewLine,
                    compiled.FunctionDefinitions.Values.Select(body => body.Dump())));
            var normalizedModule = compiled.RunPass(new FunctionToOperationPass())
                .RunPass(new StablePointerRegionParameterPass());
            capture.Write(
                "02-normalized-region.txt",
                string.Join(
                    Environment.NewLine,
                    normalizedModule.FunctionDefinitions.Values.Select(body => body.Dump())));
            var target = new SlangTargetLowering().Lower(normalizedModule);
            capture.Write(
                "03-target-ast.txt",
                string.Join(
                    Environment.NewLine,
                    target.FunctionDefinitions.Values.Select(body => body.PrettyPrint())));
            var slang = slangCompiler.Emit(shader);
            var noExitBlocks = compiled.FunctionDefinitions.Values
                .SelectMany(body => body.Labels.Select(label => body[label]))
                .Where(block => block.PostDominance is ExitPostDominance.NoExitPath)
                .ToArray();
            Assert.NotEmpty(noExitBlocks);
            Assert.Contains("while(true)", slang);

            var error = Assert.Throws<NotSupportedException>(() =>
                new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader));
            Assert.Contains("WGSL output does not support", error.Message);
            Assert.Contains("no finite exit path", error.Message);
            capture.Write("04-target.slang", slang);
            capture.Write("05-wgsl-diagnostic.txt", error.ToString());

            var method = shader switch
            {
                CorpusMixedDivergenceShader =>
                    ((Func<bool, int>)CorpusMixedDivergenceShader.MixedReturnDivergence).Method,
                CorpusNoExitShader => ((Func<bool, int>)CorpusNoExitShader.NoExit).Method,
                _ => throw new InvalidOperationException($"Unknown divergence shader {shader.GetType().Name}.")
            };
            var stages = CompilerTestPipeline.CompileStages(method);
            var value = Assert.Single(stages.ValueControlFlow.FunctionDefinitions.Values);
            var facts = Assert.Single(stages.ControlFacts.FunctionDefinitions.Values);
            var region = Assert.Single(stages.Compiled.FunctionDefinitions.Values);
            var normalized = new StablePointerRegionParameterPass().VisitFunctionBody(
                new FunctionToOperationPass().VisitFunctionBody(region));
            var scalarSource = ScalarControlFlowTests.Emit(normalized);
            capture.Write("06-value-cfg.txt", value.PrettyPrint());
            capture.Write("07-control-facts.txt", facts.PrettyPrint());
            capture.Write("08-scalar-region.txt", region.Dump());
            capture.Write("09-scalar-target.slang", scalarSource);

            if (shader is CorpusMixedDivergenceShader)
            {
                var returning = ImmutableArray.Create<Value>(new Value.Boolean(true));
                var valueResult = RunValueCfg(value, returning);
                var factsResult = RunFactsCfg(facts, returning);
                var regionResult = RunCfg(region, returning);
                var scopedResult = ScopedContinuationOracle.RunScoped(region, returning);
                var emittedResult = new EmittedScalarProgram(normalized, scalarSource).Run(returning);
                Assert.Equal(new Value.Integer(7), valueResult.Result);
                AssertEquivalent(valueResult, factsResult);
                AssertEquivalent(valueResult, regionResult);
                AssertEquivalent(valueResult, scopedResult);
                AssertEquivalent(valueResult, emittedResult);
                capture.Write(
                    "10-returning-execution.txt",
                    $"value result={valueResult.Result} trace={string.Join(" -> ", valueResult.Trace)}{Environment.NewLine}" +
                    $"facts result={factsResult.Result} trace={string.Join(" -> ", factsResult.Trace)}{Environment.NewLine}" +
                    $"region result={regionResult.Result} trace={string.Join(" -> ", regionResult.Trace)}{Environment.NewLine}" +
                    $"scoped result={scopedResult.Result} trace={string.Join(" -> ", scopedResult.Trace)}{Environment.NewLine}" +
                    $"emitted result={emittedResult.Result} trace={string.Join(" -> ", emittedResult.Trace)}{Environment.NewLine}");
            }

            var diverging = ImmutableArray.Create<Value>(
                new Value.Boolean(shader is CorpusNoExitShader));
            capture.Write(
                "11-budget-exhaustion.txt",
                $"value: {BudgetFailure(() => RunValueCfg(value, diverging, 100))}{Environment.NewLine}" +
                $"facts: {BudgetFailure(() => RunFactsCfg(facts, diverging, 100))}{Environment.NewLine}" +
                $"region: {BudgetFailure(() => RunCfg(region, diverging, 100))}{Environment.NewLine}" +
                $"scoped: {BudgetFailure(() => ScopedContinuationOracle.RunScoped(region, diverging, 100))}{Environment.NewLine}" +
                $"emitted: {BudgetFailure(() => new EmittedScalarProgram(normalized, scalarSource).Run(diverging, 100))}{Environment.NewLine}");
        }
    }

    private ImmutableArray<string> VerifyForwardCases(ImmutableArray<GraphSpec> cases)
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        foreach (var spec in cases)
            if (VerifyForward(spec) is { } failure)
                failures.Add(failure);
        return failures.ToImmutable();
    }

    private string? VerifyForward(GraphSpec spec)
    {
        var capture = new Capture($"generated/{spec.Id}");
        var stage = "typed-raw-build";
        try
        {
            var raw = Forward(spec);
            stage = "typed-raw-input";
            capture.Write("input.txt", raw.Input);
            stage = "raw-oracle-and-arm-witnesses";
            var witnesses = Witnesses(spec);
            var covered = new HashSet<Arm>();
            var references = ImmutableArray.CreateBuilder<ReferenceExecution>();
            var rawLines = new List<string>();
            foreach (var witness in witnesses)
            {
                var reference = Reference(spec, witness.Arguments);
                var flat = RunRaw(raw, witness.Arguments);
                AssertExecution(reference, flat, raw.Identity);
                references.Add(reference);
                covered.UnionWith(reference.Arms);
                rawLines.Add(FormatExecution(witness, flat, raw.Identity));
            }
            capture.Write("raw-execution.txt", string.Join(Environment.NewLine, rawLines));

            var required = spec.Targets
                .SelectMany((targets, source) => Enumerable.Range(0, targets.Length)
                    .Select(index => new Arm(source, index)))
                .ToHashSet();
            Assert.True(required.SetEquals(covered),
                $"{spec.Id}: missing ordered arm witnesses: {string.Join(", ", required.Except(covered))}");

            stage = "facts-region-target";
            var compiled = Compile(raw, capture);
            stage = "scoped-and-emitted-oracles";
            var executionLines = new List<string>();
            foreach (var ((witness, reference), flat) in witnesses.Zip(references)
                         .Select(pair => (pair, RunRaw(raw, pair.First.Arguments))))
            {
                AssertPipeline(compiled, witness.Arguments, reference);
                executionLines.Add(
                    $"{FormatExecution(witness, flat, raw.Identity)}{Environment.NewLine}" +
                    FormatPipeline(compiled, witness.Arguments));
            }
            capture.Write("execution.txt", string.Join(Environment.NewLine, executionLines));
            return null;
        }
        catch (Exception error)
        {
            var actualStage = error is CorpusStageException corpus ? corpus.Stage : stage;
            var failure = $"{spec.Id} encoding={spec.Encoding} stage={actualStage}: {error}";
            capture.Write("failure.txt", failure);
            return failure;
        }
    }

    private static void AssertPipeline(
        CompiledCase compiled,
        ImmutableArray<Value> arguments,
        ReferenceExecution expected,
        int stepLimit = StepLimit)
    {
        AssertExecution(expected, RunCfg(compiled.Region, arguments, stepLimit), compiled.Raw.Identity);
        AssertExecution(
            expected,
            ScopedContinuationOracle.RunScoped(compiled.Region, arguments, stepLimit),
            compiled.Raw.Identity);
        AssertExecution(expected, RunCfg(compiled.Normalized, arguments, stepLimit), compiled.Raw.Identity);
        AssertExecution(
            expected,
            new EmittedScalarProgram(compiled.Normalized, compiled.Source).Run(arguments, stepLimit),
            compiled.Raw.Identity);
    }

    private static void AssertPipeline(
        CompiledCase compiled,
        ImmutableArray<Value> arguments,
        Execution expected,
        int stepLimit = StepLimit)
    {
        var region = RunCfg(compiled.Region, arguments, stepLimit);
        var scoped = ScopedContinuationOracle.RunScoped(compiled.Region, arguments, stepLimit);
        var normalized = RunCfg(compiled.Normalized, arguments, stepLimit);
        var emitted = new EmittedScalarProgram(compiled.Normalized, compiled.Source).Run(arguments, stepLimit);
        AssertEquivalent(expected, region);
        AssertEquivalent(expected, scoped);
        AssertEquivalent(expected, normalized);
        AssertEquivalent(expected, emitted);
    }

    private static void AssertExecution(
        ReferenceExecution expected,
        Execution actual,
        ImmutableDictionary<Label, int> identity)
    {
        Assert.Equal(new Value.Integer(expected.Result), actual.Result);
        Assert.Equal(expected.Trace, actual.Trace.Select(label => identity[label]));
    }

    private static string FormatExecution(
        Witness witness,
        Execution execution,
        ImmutableDictionary<Label, int> identity) =>
        $"{witness.Id} arguments=[{string.Join(", ", witness.Arguments)}] " +
        $"result={execution.Result} trace={string.Join(" -> ", execution.Trace.Select(label => identity[label]))}";

    private static string FormatFlat(
        ImmutableArray<Value> arguments,
        Execution execution,
        ImmutableDictionary<Label, int> identity) =>
        $"arguments=[{string.Join(", ", arguments)}] result={execution.Result} " +
        $"trace={string.Join(" -> ", execution.Trace.Select(label => identity[label]))}";

    private static string FormatPipeline(
        CompiledCase compiled,
        ImmutableArray<Value> arguments,
        int stepLimit = StepLimit)
    {
        var raw = RunRaw(compiled.Raw, arguments, stepLimit);
        var region = RunCfg(compiled.Region, arguments, stepLimit);
        var scoped = ScopedContinuationOracle.RunScoped(compiled.Region, arguments, stepLimit);
        var normalized = RunCfg(compiled.Normalized, arguments, stepLimit);
        var emitted = new EmittedScalarProgram(compiled.Normalized, compiled.Source).Run(arguments, stepLimit);
        string Format(string name, Execution execution) =>
            $"{name} result={execution.Result} trace=" +
            string.Join(" -> ", execution.Trace.Select(label => compiled.Raw.Identity[label]));
        return $"arguments=[{string.Join(", ", arguments)}]{Environment.NewLine}" +
               $"{Format("raw", raw)}{Environment.NewLine}" +
               $"{Format("region", region)}{Environment.NewLine}" +
               $"{Format("scoped", scoped)}{Environment.NewLine}" +
               $"{Format("normalized", normalized)}{Environment.NewLine}" +
               $"{Format("emitted", emitted)}{Environment.NewLine}";
    }

    private static void AssertNoFailures(ImmutableArray<string> failures)
    {
        if (failures.IsEmpty)
            return;
        Assert.Fail(
            $"Corpus failures ({failures.Length}):{Environment.NewLine}" +
            string.Join(Environment.NewLine + Environment.NewLine, failures));
    }

    private static HashSet<Label> NaturalLoopMembers(
        ControlFlowGraph<Annotated<CilValueBasicBlock, BlockControlFacts>> facts,
        Label header)
    {
        var members = new HashSet<Label> { header };
        var pending = new Stack<Label>(
            facts[header].Annotation.IncomingArms
                .Where(arm => arm.IsBackedge)
                .Select(arm => arm.Source));
        while (pending.TryPop(out var member))
        {
            if (!members.Add(member))
                continue;
            foreach (var predecessor in facts.Predecessor(member))
                pending.Push(predecessor);
        }
        return members;
    }

    private static async Task ValidateNative(Capture capture, string path, string source)
    {
        try
        {
            await new SlangService().ValidateAsync(source);
            capture.Write(path, "native Slang validation passed" + Environment.NewLine);
        }
        catch (Exception error)
        {
            capture.Write(path, error.ToString());
            throw;
        }
    }

    private static string BudgetFailure(Func<Execution> execute)
    {
        var error = Assert.Throws<InvalidOperationException>(() => execute());
        Assert.Contains("step budget", error.Message);
        return error.Message;
    }
}

public sealed class CorpusOrdinaryShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int Fragment([Location(0)] int value) => OrdinaryControl(value);

    [ShaderMethod]
    public static int OrdinaryControl(int value)
    {
        var result = 17;
        for (var index = 0; index < value; index++)
        {
            if (index == 1)
                continue;
            result += index + 1;
        }
        return result;
    }
}

public sealed class CorpusDenseSwitchShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int Fragment([Location(0)] int value) => DenseSwitch(value);

    [ShaderMethod]
    public static int DenseSwitch(int value) =>
        value switch
        {
            0 => 11,
            1 => 13,
            2 => 17,
            3 => 19,
            4 => 23,
            5 => 29,
            6 => 31,
            7 => 37,
            _ => 41
        };
}

public sealed class CorpusMixedDivergenceShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int Fragment([Location(0)] int value) => MixedReturnDivergence(value > 0);

    [ShaderMethod]
    public static int MixedReturnDivergence(bool shouldReturn)
    {
        if (shouldReturn)
            return 7;
        while (true)
            shouldReturn = !shouldReturn;
    }
}

public sealed class CorpusNoExitShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int Fragment([Location(0)] int value) => NoExit(value > 0);

    [ShaderMethod]
    public static int NoExit(bool state)
    {
        while (true)
            state = !state;
    }
}
