using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Backend.Wasm;
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
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using static DualDrill.CLSL.Test.RegionFixture;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using static DualDrill.CLSL.Test.ScopedContinuationOracle;
using Label = DualDrill.CLSL.Language.Symbol.Label;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CilSwitchTests(ITestOutputHelper output)
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    public static TheoryData<int, int> DenseCases =>
        new()
        {
            { int.MinValue, 99 },
            { -1, 99 },
            { 0, 10 },
            { 1, 20 },
            { 2, 30 },
            { 3, 40 },
            { 4, 99 },
            { int.MaxValue, 99 }
        };

    private static readonly (int Selector, int Expected)[] DenseExpectations =
    [
        (int.MinValue, 99),
        (-1, 99),
        (0, 10),
        (1, 20),
        (2, 30),
        (3, 40),
        (4, 99),
        (int.MaxValue, 99)
    ];

    [Theory]
    [MemberData(nameof(DenseCases))]
    public void DenseCSharpSwitchRunsThroughEverySharedStage(int selector, int expected)
    {
        var method = GetMethod(nameof(DenseSwitch));
        Assert.Equal(expected, DenseSwitch(selector));
        Assert.Contains(
            CilMethodDecoder.Decode(method).Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Switch);

        var stages = CompilerTestPipeline.CompileStages(method);
        var labelled = ModuleBody(stages.Labelled);
        var switchBlock = Assert.Single(
            labelled.Blocks.Blocks,
            block => block.Terminator is CilControlFlow.Switch);
        var cilSwitch = Assert.IsType<CilControlFlow.Switch>(switchBlock.Terminator);
        Assert.Equal(4, cilSwitch.CaseTargets.Length);

        var shader = ModuleBody(stages.ShaderStack);
        var shaderSwitch = Assert.IsType<Terminator.D.Switch<Label, ShaderStackOperand>>(
            shader.Blocks.Blocks.Single(block => ReferenceEquals(block.Label, switchBlock.Label)).Body.Last.Node);
        Assert.Equal(ShaderType.I32, shaderSwitch.Selector.Type);
        Assert.IsType<ShaderStackOperand.Depth>(shaderSwitch.Selector);

        var value = ModuleBody(stages.ValueControlFlow);
        var valueSwitch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            value.Graph[switchBlock.Label].Body.Last);
        Assert.Equal(ShaderType.I32, valueSwitch.Selector.Type);
        Assert.Equal(4, valueSwitch.CaseTargets.Length);

        var promoted = ModuleBody(stages.PromotedValueControlFlow);
        var facts = ModuleBody(stages.ControlFacts);
        var region = ModuleBody(stages.Compiled);
        var arguments = ImmutableArray.Create<Value>(new Value.Integer(selector));
        var golden = new Value.Integer(expected);
        var valueExecution = RunValueCfg(value, arguments);
        var promotedExecution = RunValueCfg(promoted, arguments);
        var factsExecution = RunFactsCfg(facts, arguments);
        var cfgExecution = RunCfg(region, arguments);
        var scopedExecution = RunScoped(region, arguments);
        Assert.Equal(golden, valueExecution.Result);
        AssertEquivalent(valueExecution, promotedExecution);
        AssertEquivalent(valueExecution, factsExecution);
        AssertEquivalent(valueExecution, cfgExecution);
        AssertEquivalent(valueExecution, scopedExecution);
    }

    [Fact]
    public async Task DenseCSharpSwitchLowersToNestedIfAndCompilesWithPinnedSlang()
    {
        var method = GetMethod(nameof(DenseSwitch));
        var region = ModuleBody(CompilerTestPipeline.CompileStages(method).Compiled);
        var target = ScalarControlFlowTests.Lower(region);
        var source = ScalarControlFlowTests.Emit(target);

        Assert.DoesNotContain("\nswitch ", source, StringComparison.OrdinalIgnoreCase);
        Assert.True(Statements(target.Body).OfType<SlangIf>().Count() >= 4);
        var switchSelector = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            Assert.Single(region.Labels.Select(label => region[label].Body.Last)
                .OfType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>()));
        Assert.Single(
            Statements(target.Body).OfType<SlangBind>(),
            binding => ReferenceEquals(binding.Instruction.Result, switchSelector.Selector));
        foreach (var (selector, expected) in DenseExpectations)
        {
            var execution = new EmittedScalarProgram(region, source)
                .Run([new Value.Integer(selector)]);
            Assert.Equal(new Value.Integer(expected), execution.Result);
        }
        await CompileWgslAsync(source);
    }

    [Fact]
    public void NativeSwitchCompilesThroughPublicApis()
    {
        var shader = new NativeSwitchShader();
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var raw = Assert.Single(
            compiler.Parse(shader).FunctionDefinitions.Values,
            body => body.Code.Environment.Method.Name == nameof(DenseSwitch));
        Assert.Contains(raw.Code.Instructions, instruction => instruction.Instruction.OpCode == OpCodes.Switch);
        var ir = compiler.Emit(shader);
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);

        Assert.Contains("switch ", ir);
        Assert.Contains("case 0 -> ", ir);
        Assert.Contains("default -> ", ir);
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("fn fs", wgsl);
        Capture("native-public/raw CIL", raw.PrettyPrint());
        Capture("native-public/IR", ir);
        Capture("native-public/Slang", slang);
        Capture("native-public/WGSL", wgsl);
    }

    [Theory]
    [InlineData("effects", -1, 11100)]
    [InlineData("effects", 0, 11011)]
    [InlineData("effects", 1, 11021)]
    [InlineData("effects", 2, 30)]
    [InlineData("effects", 3, 11041)]
    [InlineData("loop", 0, 0)]
    [InlineData("loop", 4, 60)]
    [InlineData("loop", 8, 70)]
    [InlineData("nested-false", 2, -1)]
    [InlineData("nested-true", 2, 30)]
    public void NativeSwitchEffectsReturnsLoopsAndNestingMatchAllOracles(
        string scenario,
        int input,
        int expected)
    {
        var (method, arguments, cpu) = scenario switch
        {
            "effects" => (
                GetMethod(nameof(SwitchEffectsAndEarlyReturn)),
                ImmutableArray.Create<Value>(new Value.Integer(input)),
                SwitchEffectsAndEarlyReturn(input)),
            "loop" => (
                GetMethod(nameof(SwitchLoop)),
                ImmutableArray.Create<Value>(new Value.Integer(input)),
                SwitchLoop(input)),
            "nested-false" => (
                GetMethod(nameof(NestedConditionSwitch)),
                ImmutableArray.Create<Value>(new Value.Boolean(false), new Value.Integer(input)),
                NestedConditionSwitch(false, input)),
            "nested-true" => (
                GetMethod(nameof(NestedConditionSwitch)),
                ImmutableArray.Create<Value>(new Value.Boolean(true), new Value.Integer(input)),
                NestedConditionSwitch(true, input)),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        Assert.Equal(expected, cpu);
        Assert.Contains(
            CilMethodDecoder.Decode(method).Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Switch);

        var stages = CompilerTestPipeline.CompileStages(method);
        var value = RunValueCfg(ModuleBody(stages.ValueControlFlow), arguments);
        var facts = RunFactsCfg(ModuleBody(stages.ControlFacts), arguments);
        var cfg = RunCfg(ModuleBody(stages.Compiled), arguments);
        var scoped = RunScoped(ModuleBody(stages.Compiled), arguments);
        var configuration = GetType().Assembly
                                     .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
                            ?? throw new InvalidOperationException("Assembly configuration was not recorded.");
        output.WriteLine(
            $"{configuration} {scenario}({input}) result={value.Result} " +
            $"trace={string.Join(" -> ", value.Trace.Select(label => label.Name))}");
        var expectedTrace = ExpectedNativeTrace(configuration, scenario, input);
        Assert.Equal(new Value.Integer(expected), value.Result);
        Assert.Equal(expectedTrace, value.Trace.Select(label => label.Name));
        AssertEquivalent(value, facts);
        AssertEquivalent(value, cfg);
        AssertEquivalent(value, scoped);
        var source = ScalarControlFlowTests.Emit(ScalarControlFlowTests.Lower(ModuleBody(stages.Compiled)));
        var emitted = new EmittedScalarProgram(ModuleBody(stages.Compiled), source).Run(arguments);
        AssertEquivalent(cfg, emitted);
    }

    [Fact]
    public void ExplicitCilTablesPreserveEmptyDuplicateDefaultAndRetainedStackSemantics()
    {
        var duplicate = CompilerTestPipeline.CompileStages(Fixtures.DuplicateTargets);
        var duplicateLabelled = ModuleBody(duplicate.Labelled);
        var duplicateBlock = Assert.Single(
            duplicateLabelled.Blocks.Blocks,
            block => block.Terminator is CilControlFlow.Switch);
        var duplicateSwitch = Assert.IsType<CilControlFlow.Switch>(duplicateBlock.Terminator);
        Assert.Equal(
            [
                duplicateSwitch.CaseTargets[0],
                duplicateSwitch.CaseTargets[0],
                duplicateSwitch.CaseTargets[2],
                duplicateSwitch.DefaultTarget
            ],
            duplicateSwitch.ToSuccessor().AllTargets());
        Assert.Same(duplicateSwitch.CaseTargets[0], duplicateSwitch.CaseTargets[1]);
        Assert.Same(duplicateSwitch.CaseTargets[0], duplicateSwitch.DefaultTarget);
        Assert.Single(ModuleBody(duplicate.ShaderControlFlow).Graph.Predecessor(duplicateSwitch.DefaultTarget));
        var duplicateFacts = ModuleBody(duplicate.ControlFacts);
        Assert.Equal(
            [0, 1, 3],
            duplicateFacts.Graph[duplicateSwitch.DefaultTarget].Annotation.IncomingArms
                .Select(arm => arm.SuccessorIndex));
        Assert.All(
            duplicateFacts.Graph[duplicateSwitch.DefaultTarget].Annotation.IncomingArms,
            arm => Assert.Same(duplicateBlock.Label, arm.Source));
        var duplicateRegion = ModuleBody(duplicate.Compiled);
        Assert.Same(
            duplicateSwitch.DefaultTarget,
            duplicateRegion.Control.Resolve(duplicateBlock.Label, 3).Target);

        foreach (var (selector, expected) in new[]
                 {
                     (-1, 99), (0, 99), (1, 99), (2, 20), (3, 99)
                 })
        {
            Assert.Equal(expected, Fixtures.Invoke(Fixtures.DuplicateTargets, selector));
            Assert.Equal(
                new Value.Integer(expected),
                RunCfg(ModuleBody(duplicate.Compiled),
                    [new Value.Integer(selector)]).Result);
        }

        var empty = CompilerTestPipeline.CompileStages(Fixtures.Empty);
        var emptySwitch = Assert.IsType<CilControlFlow.Switch>(
            Assert.Single(ModuleBody(empty.Labelled).Blocks.Blocks,
                block => block.Terminator is CilControlFlow.Switch).Terminator);
        Assert.Empty(emptySwitch.CaseTargets);
        Assert.Single(emptySwitch.ToSuccessor().AllTargets());
        var emptyRegion = ModuleBody(empty.Compiled);
        var emptyValueSwitch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            emptyRegion[emptyRegion.Entry].Body.Last);
        Assert.Empty(Statements(ScalarControlFlowTests.Lower(emptyRegion).Body)
            .OfType<SlangIf>());
        Assert.Empty(emptyValueSwitch.CaseTargets);
        Assert.Same(
            emptyValueSwitch.DefaultTarget.Label,
            emptyRegion.Control.Resolve(emptyRegion.Entry, 0).Target);
        var emptySource = ScalarControlFlowTests.Emit(ScalarControlFlowTests.Lower(emptyRegion));
        foreach (var selector in new[] { int.MinValue, -1, 0, 1, 2, int.MaxValue })
        {
            var arguments = ImmutableArray.Create<Value>(new Value.Integer(selector));
            var expected = new Value.Integer(99);
            var value = RunValueCfg(ModuleBody(empty.ValueControlFlow), arguments);
            var facts = RunFactsCfg(ModuleBody(empty.ControlFacts), arguments);
            var cfg = RunCfg(emptyRegion, arguments);
            var scoped = RunScoped(emptyRegion, arguments);
            var emitted = new EmittedScalarProgram(emptyRegion, emptySource).Run(arguments);
            Assert.Equal(99, Fixtures.Invoke(Fixtures.Empty, selector));
            Assert.Equal(expected, value.Result);
            AssertEquivalent(value, facts);
            AssertEquivalent(value, cfg);
            AssertEquivalent(value, scoped);
            AssertEquivalent(value, emitted);
        }

        var retained = CompilerTestPipeline.CompileStages(Fixtures.RetainedStack);
        var retainedLabelled = ModuleBody(retained.Labelled);
        var retainedBlock = Assert.Single(
            retainedLabelled.Blocks.Blocks,
            block => block.Terminator is CilControlFlow.Switch);
        Assert.Collection(
            retainedBlock.Instructions,
            instruction => Assert.Empty(instruction.Annotation.Types),
            instruction => Assert.Collection(
                instruction.Annotation.Types.Reverse(),
                type => Assert.IsType<CilStackType.Int32>(type)),
            instruction => Assert.Collection(
                instruction.Annotation.Types.Reverse(),
                type => Assert.IsType<CilStackType.Int32>(type),
                type => Assert.IsType<CilStackType.Int32>(type)),
            instruction => Assert.Collection(
                instruction.Annotation.Types.Reverse(),
                type => Assert.IsType<CilStackType.Int32>(type),
                type => Assert.IsType<CilStackType.Int32>(type),
                type => Assert.IsType<CilStackType.Int32>(type)));
        var retainedShader = ModuleBody(retained.ShaderStack);
        var retainedShaderBlock = retainedShader.Blocks.Blocks
            .Single(block => ReferenceEquals(block.Label, retainedBlock.Label));
        var retainedTerminator = Assert.IsType<Terminator.D.Switch<Label, ShaderStackOperand>>(
            retainedShaderBlock.Body.Last.Node);
        var retainedTransition = retainedShaderBlock.Body.Last.Annotation;
        Assert.Equal(ShaderType.I32, retainedTerminator.Selector.Type);
        Assert.Equal(
            [
                (ImmutableArray<IShaderType>)[],
                [ShaderType.I32],
                [ShaderType.I32, ShaderType.I32]
            ],
            retainedShaderBlock.Body.Elements.Select(instruction => instruction.Annotation.Pre));
        Assert.Equal(
            [
                (ImmutableArray<IShaderType>)[ShaderType.I32],
                [ShaderType.I32, ShaderType.I32],
                [ShaderType.I32, ShaderType.I32, ShaderType.I32]
            ],
            retainedShaderBlock.Body.Elements.Select(instruction => instruction.Annotation.Post));
        Assert.True(retainedTransition.Pre.SequenceEqual(
            [ShaderType.I32, ShaderType.I32, ShaderType.I32]));
        Assert.True(retainedTransition.Post.SequenceEqual([ShaderType.I32, ShaderType.I32]));
        Assert.True(retainedShaderBlock.ExitStack.SequenceEqual([ShaderType.I32, ShaderType.I32]));
        var retainedValueBlock = ModuleBody(retained.ValueControlFlow).Graph[retainedBlock.Label];
        var retainedValueSwitch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            retainedValueBlock.Body.Last);
        var retainedLoads = retainedValueBlock.Body.Elements
            .Where(instruction => instruction.Operation is LoadOperation)
            .Select(instruction => Assert.IsAssignableFrom<IShaderValue>(instruction.Result))
            .ToArray();
        Assert.Equal(3, retainedLoads.Length);
        Assert.Same(retainedLoads[2], retainedValueSwitch.Selector);
        Assert.All(
            retainedValueSwitch.CaseTargets.Append(retainedValueSwitch.DefaultTarget),
            target =>
            {
                Assert.Equal(2, target.Arguments.Length);
                Assert.Same(retainedLoads[0], target.Arguments[0]);
                Assert.Same(retainedLoads[1], target.Arguments[1]);
                Assert.True(target.Arguments.Select(value => value.Type)
                    .SequenceEqual([ShaderType.I32, ShaderType.I32]));
            });

        var retainedRegion = ModuleBody(retained.Compiled);
        var retainedSource = ScalarControlFlowTests.Emit(ScalarControlFlowTests.Lower(retainedRegion));
        foreach (var (selector, expected) in new[] { (-1, -263), (0, -23), (1, -53), (2, -263) })
        {
            var arguments = ImmutableArray.Create<Value>(
                new Value.Integer(7),
                new Value.Integer(3),
                new Value.Integer(selector));
            var golden = new Value.Integer(expected);
            var value = RunValueCfg(ModuleBody(retained.ValueControlFlow), arguments);
            var facts = RunFactsCfg(ModuleBody(retained.ControlFacts), arguments);
            var cfg = RunCfg(retainedRegion, arguments);
            var scoped = RunScoped(retainedRegion, arguments);
            var emitted = new EmittedScalarProgram(retainedRegion, retainedSource).Run(arguments);
            Assert.Equal(expected, Fixtures.Invoke(Fixtures.RetainedStack, 7, 3, selector));
            Assert.Equal(golden, value.Result);
            AssertEquivalent(value, facts);
            AssertEquivalent(value, cfg);
            AssertEquivalent(value, scoped);
            AssertEquivalent(value, emitted);
        }
    }

    [Fact]
    public async Task CrossLabelSwitchCapturesSelectorAndDistinctTuplesAndCompilesActualWgsl()
    {
        var body = CrossLabelSwitchBody();
        var entry = LabelNamed(body, "entry");
        var dispatch = LabelNamed(body, "dispatch");
        var join = LabelNamed(body, "join");
        var branch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            body[dispatch].Body.Last);
        var tupleValues = body[entry].Body.Elements
            .Where(instruction => instruction.Operation is LiteralOperation)
            .Select(instruction => Assert.IsAssignableFrom<IShaderValue>(instruction.Result))
            .ToArray();
        Assert.All(branch.CaseTargets, target => Assert.Same(join, target.Label));
        Assert.Same(join, branch.DefaultTarget.Label);
        Assert.Equal(3, tupleValues.Length);
        Assert.Same(tupleValues[0], branch.CaseTargets[0].Arguments[0]);
        Assert.Same(tupleValues[1], branch.CaseTargets[1].Arguments[0]);
        Assert.Same(tupleValues[2], branch.DefaultTarget.Arguments[0]);

        var target = ScalarControlFlowTests.Lower(body);
        var captures = target.Body.Statements
            .OfType<SlangDeclare>()
            .Where(statement => statement.Variable.Name.StartsWith("capture_", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, captures.Length);
        var source = ScalarControlFlowTests.Emit(target);
        foreach (var (selector, expected) in new[]
                 {
                     (int.MinValue, 99), (-1, 99), (0, 10), (1, 20), (2, 99), (int.MaxValue, 99)
                 })
        {
            var arguments = ImmutableArray.Create<Value>(new Value.Integer(selector));
            var expectedTrace = new[] { entry, dispatch, join };
            var cfg = RunCfg(body, arguments);
            var scoped = RunScoped(body, arguments);
            var emitted = new EmittedScalarProgram(body, source).Run(arguments);
            Assert.Equal(new Value.Integer(expected), cfg.Result);
            Assert.Equal(expectedTrace, cfg.Trace);
            AssertEquivalent(cfg, scoped);
            AssertEquivalent(cfg, emitted);
        }

        var wgsl = await CompileWgslAsync(
            ScalarControlFlowTests.Emit(ScalarControlFlowTests.Lower(CrossLabelSwitchBody(entryPoint: true))));
        Assert.False(string.IsNullOrWhiteSpace(wgsl));
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("fn CrossLabelSwitch", wgsl);
    }

    [Fact]
    public async Task SwitchAcceptanceStagesAndHandIrAreCaptured()
    {
        var stages = CompilerTestPipeline.CompileStages(Fixtures.RetainedStack);
        var region = ModuleBody(stages.Compiled);
        var target = ScalarControlFlowTests.Lower(region);
        var slang = ScalarControlFlowTests.Emit(target);
        Capture("retained/raw CIL", ModuleBody(stages.Raw).PrettyPrint());
        Capture("retained/Pre CIL", ModuleBody(stages.Pre).PrettyPrint());
        Capture("retained/labelled CIL", ModuleBody(stages.Labelled).PrettyPrint());
        Capture("retained/shader stack", ModuleBody(stages.ShaderStack).PrettyPrint());
        Capture("retained/shader stack CFG", ModuleBody(stages.ShaderControlFlow).PrettyPrint());
        Capture("retained/value CFG", ModuleBody(stages.ValueControlFlow).PrettyPrint());
        Capture("retained/promoted value CFG", ModuleBody(stages.PromotedValueControlFlow).PrettyPrint());
        Capture("retained/control facts", ModuleBody(stages.ControlFacts).PrettyPrint());
        Capture("retained/checked regions", region.Dump());
        Capture("retained/target AST", target.PrettyPrint());
        Capture("retained/Slang", slang);
        output.WriteLine("=== retained/input-output ===");
        foreach (var (selector, expected) in new[] { (-1, -263), (0, -23), (1, -53), (2, -263) })
        {
            var actual = Fixtures.Invoke(Fixtures.RetainedStack, 7, 3, selector);
            Assert.Equal(expected, actual);
            output.WriteLine($"a=7 b=3 selector={selector} CPU result={actual}");
        }

        foreach (var (name, body, cases) in new[]
                 {
                     (
                         "same-target",
                         DistinctTupleBody(),
                         new[]
                         {
                             (ImmutableArray.Create<Value>(new Value.Integer(-1)), 99),
                             (ImmutableArray.Create<Value>(new Value.Integer(0)), 10),
                             (ImmutableArray.Create<Value>(new Value.Integer(1)), 20),
                             (ImmutableArray.Create<Value>(new Value.Integer(2)), 99)
                         }),
                     (
                         "cyclic",
                         CyclicSwitchBody(),
                         new[] { (ImmutableArray<Value>.Empty, 200101) }),
                     (
                         "cross-label",
                         CrossLabelSwitchBody(),
                         new[]
                         {
                             (ImmutableArray.Create<Value>(new Value.Integer(-1)), 99),
                             (ImmutableArray.Create<Value>(new Value.Integer(0)), 10),
                             (ImmutableArray.Create<Value>(new Value.Integer(1)), 20),
                             (ImmutableArray.Create<Value>(new Value.Integer(2)), 99)
                         })
                 })
        {
            var handTarget = ScalarControlFlowTests.Lower(body);
            var handSlang = ScalarControlFlowTests.Emit(handTarget);
            Capture($"{name}/regions", body.Dump());
            Capture($"{name}/target AST", handTarget.PrettyPrint());
            Capture($"{name}/Slang", handSlang);
            output.WriteLine($"=== {name}/input-output ===");
            foreach (var (arguments, expected) in cases)
            {
                var execution = RunCfg(body, arguments);
                Assert.Equal(new Value.Integer(expected), execution.Result);
                output.WriteLine(
                    $"arguments={string.Join(", ", arguments)} result={execution.Result} " +
                    $"trace={string.Join(" -> ", execution.Trace.Select(label => label.Name))}");
            }
        }

        var crossLabelTarget = ScalarControlFlowTests.Lower(CrossLabelSwitchBody(entryPoint: true));
        var fragmentSlang = ScalarControlFlowTests.Emit(crossLabelTarget);
        Capture("cross-label/fragment Slang", fragmentSlang);
        var wgsl = await CompileWgslAsync(fragmentSlang);
        Capture("cross-label/WGSL", wgsl);
        Assert.False(string.IsNullOrWhiteSpace(wgsl));
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("fn CrossLabelSwitch", wgsl);
    }

    [Fact]
    public void SwitchSelectionPreservesDistinctTuplesToOneLabel()
    {
        var body = DistinctTupleBody();
        var entry = body.Entry;
        var branch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            body[entry].Body.Last);
        Assert.All(branch.CaseTargets, target => Assert.Same(branch.DefaultTarget.Label, target.Label));
        Assert.Equal([10, 20], branch.CaseTargets.Select(target => LiteralInt(target.Arguments[0])));
        Assert.Equal(99, LiteralInt(branch.DefaultTarget.Arguments[0]));

        foreach (var (selector, expected) in new[]
                 {
                     (int.MinValue, 99), (-1, 99), (0, 10), (1, 20), (2, 99), (int.MaxValue, 99)
                 })
        {
            var arguments = ImmutableArray.Create<Value>(new Value.Integer(selector));
            var cfg = RunCfg(body, arguments);
            var scoped = RunScoped(body, arguments);
            Assert.Equal(new Value.Integer(expected), cfg.Result);
            Assert.True(cfg.Trace.SequenceEqual([entry, branch.DefaultTarget.Label]));
            AssertEquivalent(cfg, scoped);
        }
    }

    [Fact]
    public void SwitchLoopUsesSelectedEdgeParallelCopyAndCheckedArmIdentity()
    {
        var body = CyclicSwitchBody();
        var entry = LabelNamed(body, "entry");
        var loop = LabelNamed(body, "loop");
        var exit = LabelNamed(body, "exit");
        var branch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            body[loop].Body.Last);

        Assert.Same(body[loop].Parameters[1], branch.CaseTargets[0].Arguments[0]);
        Assert.Same(body[loop].Parameters[0], branch.CaseTargets[0].Arguments[1]);
        Assert.Equal(1, LiteralInt(branch.CaseTargets[0].Arguments[2]));
        Assert.Equal(ScopedContinuationKind.Repeat, body.Control.Resolve(loop, 0).Kind);
        Assert.Equal(ScopedContinuationKind.Forward, body.Control.Resolve(loop, 1).Kind);

        var cfg = RunCfg(body, []);
        Assert.Equal(new Value.Integer(200101), cfg.Result);
        Assert.True(cfg.Trace.SequenceEqual([entry, loop, loop, exit]));
        AssertEquivalent(cfg, RunScoped(body, []));
        var lowered = ScalarControlFlowTests.Lower(body);
        var source = ScalarControlFlowTests.Emit(lowered);
        AssertEquivalent(cfg, new EmittedScalarProgram(body, source).Run([]));
    }

    [Fact]
    public void SwitchSelectMapsSelectorCasesAndDefaultExactlyOnceInOrder()
    {
        var selector = new object();
        var case0 = new object();
        var case1 = new object();
        var @default = new object();
        var visited = new List<object>();
        var original = new Terminator.D.Switch<object, object>(
            selector,
            [case0, case1],
            @default);

        var selected = Assert.IsType<Terminator.D.Switch<object, object>>(
            original.Select(
                target =>
                {
                    visited.Add(target);
                    return target;
                },
                value =>
                {
                    visited.Add(value);
                    return value;
                }));

        Assert.Equal([selector, case0, case1, @default], visited);
        Assert.Same(selector, selected.Selector);
        Assert.Same(case0, selected.CaseTargets[0]);
        Assert.Same(case1, selected.CaseTargets[1]);
        Assert.Same(@default, selected.DefaultTarget);
        Assert.Throws<ArgumentException>(() =>
            new Terminator.D.Switch<object, object>(selector, default, @default));
        Assert.Throws<ArgumentException>(() => new SwitchSuccessor(default, Label.Create("default")));
    }

    [Fact]
    public void SwitchSuccessorEqualityIsStructuralOrderedAndDuplicateSensitive()
    {
        var a = Label.Create("a");
        var b = Label.Create("b");
        var left = new SwitchSuccessor([a, a, b], a);
        var equal = new SwitchSuccessor([a, a, b], a);

        Assert.Equal(left, equal);
        Assert.Equal(left.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(left, new SwitchSuccessor([a, b, a], a));
        Assert.NotEqual(left, new SwitchSuccessor([a, b], a));
        Assert.Equal([a, a, b, a], left.AllTargets());
    }

    [Theory]
    [InlineData("underflow")]
    [InlineData("float")]
    [InlineData("i64")]
    public void InvalidCilSwitchSelectorFailsStrictPre(string fixture)
    {
        var method = fixture switch
        {
            "underflow" => Fixtures.Underflow,
            "float" => Fixtures.FloatSelector,
            "i64" => Fixtures.Int64Selector,
            _ => throw new ArgumentOutOfRangeException(nameof(fixture))
        };
        var exception = Assert.Throws<ValidationException>(() =>
            CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)));
        Assert.Contains("IL_", exception.Message);
        Assert.Contains(method.Name, exception.Message);
    }

    [Fact]
    public void SwitchMergeRejectsIncompatibleRetainedStackTypes()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(Fixtures.MergeMismatch)));
        Assert.Contains("stack type mismatch", exception.Message);
        Assert.Contains(Fixtures.MergeMismatch.Name, exception.Message);
    }

    [Theory]
    [InlineData("overflow")]
    [InlineData("mid-instruction")]
    [InlineData("end-of-code")]
    public void MalformedDeadSwitchTargetsFailEagerBoundaryValidation(string failure)
    {
        var module = CompilerTestPipeline.ParseRaw(Fixtures.DeadMalformed);
        var raw = CompilerTestPipeline.RawBody(module, Fixtures.DeadMalformed);
        var @switch = Assert.Single(
            raw.Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Switch);
        var offsets = Assert.IsType<int[]>(@switch.Instruction.Operand);
        offsets[0] = failure switch
        {
            "overflow" => int.MaxValue,
            "mid-instruction" => raw.Code.Instructions
                .First(instruction => instruction.NextByteOffset - instruction.ByteOffset > 1)
                .ByteOffset + 1 - @switch.NextByteOffset,
            "end-of-code" => raw.Code.Environment.CodeByteSize - @switch.NextByteOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };

        var exception = Assert.Throws<InvalidProgramException>(() => CilPreStackPass.Run(module));
        Assert.Contains($"IL_{@switch.ByteOffset:X4}", exception.Message);
        Assert.Contains("case 0", exception.Message);
    }

    [Fact]
    public void MalformedDeadSwitchOperandFailsEagerBoundaryValidation()
    {
        var module = CompilerTestPipeline.ParseRaw(Fixtures.DeadMalformed);
        var raw = CompilerTestPipeline.RawBody(module, Fixtures.DeadMalformed);
        var @switch = Assert.Single(
            raw.Code.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Switch);
        var operand = @switch.Instruction.GetType().GetField(
            "<Operand>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Decoded CIL operand backing field was not found.");
        operand.SetValue(@switch.Instruction, 0);

        var exception = Assert.Throws<InvalidProgramException>(() => CilPreStackPass.Run(module));
        Assert.Contains($"IL_{@switch.ByteOffset:X4}", exception.Message);
        Assert.Contains("expected int[]", exception.Message);
    }

    [Fact]
    public void SwitchWithoutPhysicalDefaultFailsEagerBoundaryValidation()
    {
        var exception = Assert.Throws<InvalidProgramException>(() =>
            CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(Fixtures.MissingDefault)));
        Assert.Contains("fallthrough/default", exception.Message);
        Assert.Contains(Fixtures.MissingDefault.Name, exception.Message);
    }

    [Fact]
    public void RegionValidationRejectsWrongSwitchTypeAndMalformedTuples()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var declaration = Function("InvalidSwitch", ShaderType.I32);
        var boolean = ShaderValue.Intermediate(ShaderType.Bool);
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var wrongType = Body(entry, [], [],
            Terms.Switch(boolean, [], new RegionJump<IShaderValue>(exit, [Int(1)])));
        var exitBody = Body(exit, [parameter], [], Terms.ReturnExpr(parameter));
        Assert.Throws<ArgumentException>(() => CreateFunctionBody(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(exit, [], exitBody, null)], wrongType, exit)));

        var selector = ShaderValue.Intermediate(ShaderType.I32);
        var wrongArity = Body(entry, [], [],
            Terms.Switch(selector, [], new RegionJump<IShaderValue>(exit, [])));
        Assert.Throws<ArgumentException>(() => CreateFunctionBody(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(exit, [], exitBody, null)], wrongArity, exit)));

        var wrongArgumentType = Body(entry, [], [],
            Terms.Switch(selector, [], new RegionJump<IShaderValue>(exit, [Bool(true)])));
        Assert.Throws<ArgumentException>(() => CreateFunctionBody(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(exit, [], exitBody, null)], wrongArgumentType, exit)));
    }

    [Fact]
    public void UnsupportedBackendsRejectSwitchExplicitly()
    {
        var body = DistinctTupleBody();
        var branch = Assert.IsType<Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>>(
            body[body.Entry].Body.Last);
        var wasm = Assert.Throws<WasmLoweringException>(() => WasmLowering.Lower(body));
        Assert.Contains("switch terminators are outside the bounded scalar profile", wasm.Message);

        var eager = Assert.Throws<NotSupportedException>(() =>
            new RegionParameterToLocalVariablePass().VisitFunctionBody(body));
        Assert.Contains("selected-edge stores", eager.Message);

        var spirv = Assert.Throws<NotSupportedException>(() =>
            branch.Evaluate(
                (ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, DualDrill.Common.Unit>)
                new SPIRVEmitter(Module(body))));
        Assert.Contains("SPIR-V switch lowering is not supported", spirv.Message);
    }

    private static RegionFunctionBody DistinctTupleBody()
    {
        var entry = Label.Create("entry");
        var join = Label.Create("join");
        var input = new ParameterDeclaration("selector", ShaderType.I32, []);
        var selector = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration(
            "DistinctSwitchTuple",
            [input],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var entryBody = Body(
            entry,
            [],
            [Instruction.Factory.Load(default, new LoadOperation(), selector, input.Value)],
            Terms.Switch(
                selector,
                [new(join, [Int(10)]), new(join, [Int(20)])],
                new(join, [Int(99)])));
        var joinBody = Body(join, [result], [], Terms.ReturnExpr(result));
        return CreateFunctionBody(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(join, [], joinBody, null)], entryBody, join));
    }

    private static RegionFunctionBody CyclicSwitchBody()
    {
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var exit = Label.Create("exit");
        var a = ShaderValue.Intermediate(ShaderType.I32);
        var b = ShaderValue.Intermediate(ShaderType.I32);
        var n = ShaderValue.Intermediate(ShaderType.I32);
        var outA = ShaderValue.Intermediate(ShaderType.I32);
        var outB = ShaderValue.Intermediate(ShaderType.I32);
        var outN = ShaderValue.Intermediate(ShaderType.I32);
        var scaledA = ShaderValue.Intermediate(ShaderType.I32);
        var scaledB = ShaderValue.Intermediate(ShaderType.I32);
        var sum = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("CyclicSwitch", ShaderType.I32);
        var entryBody = Body(entry, [], [], Terms.Br(new(loop, [Int(10), Int(20), Int(0)])));
        var loopBody = Body(
            loop,
            [a, b, n],
            [],
            Terms.Switch(
                n,
                [new(loop, [b, a, Int(1)])],
                new(exit, [a, b, n])));
        var exitBody = Body(
            exit,
            [outA, outB, outN],
            [
                Binary<BinaryArithmetic.Mul>(scaledA, outA, Int(10000)),
                Binary<BinaryArithmetic.Mul>(scaledB, outB, Int(10)),
                Binary<BinaryArithmetic.Add>(sum, scaledA, scaledB),
                Binary<BinaryArithmetic.Add>(result, sum, outN)
            ],
            Terms.ReturnExpr(result));
        return CreateFunctionBody(
            declaration,
            RegionTree.Block(entry,
                [
                    RegionTree.Block(exit, [], exitBody, null),
                    RegionTree.Loop(loop, [], loopBody, null, null)
                ],
                entryBody,
                loop));
    }

    private static RegionFunctionBody CrossLabelSwitchBody(bool entryPoint = false)
    {
        var entry = Label.Create("entry");
        var dispatch = Label.Create("dispatch");
        var join = Label.Create("join");
        var input = new ParameterDeclaration(
            "selector",
            ShaderType.I32,
            entryPoint ? [new LocationAttribute(0)] : []);
        var selector = ShaderValue.Intermediate(ShaderType.I32);
        var case0 = ShaderValue.Intermediate(ShaderType.I32);
        var case1 = ShaderValue.Intermediate(ShaderType.I32);
        var fallback = ShaderValue.Intermediate(ShaderType.I32);
        var result = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = new FunctionDeclaration(
            "CrossLabelSwitch",
            [input],
            new FunctionReturn(
                ShaderType.I32,
                entryPoint ? [new LocationAttribute(0)] : []),
            entryPoint ? [new FragmentAttribute()] : []);
        var entryBody = Body(
            entry,
            [],
            [
                Instruction.Factory.Load(default, new LoadOperation(), selector, input.Value),
                Instruction.Factory.Literal(default, new LiteralOperation(), case0, Int(10)),
                Instruction.Factory.Literal(default, new LiteralOperation(), case1, Int(20)),
                Instruction.Factory.Literal(default, new LiteralOperation(), fallback, Int(99))
            ],
            Terms.Br(new(dispatch, [])));
        var dispatchBody = Body(
            dispatch,
            [],
            [],
            Terms.Switch(
                selector,
                [new(join, [case0]), new(join, [case1])],
                new(join, [fallback])));
        var joinBody = Body(join, [result], [], Terms.ReturnExpr(result));
        return CreateFunctionBody(
            declaration,
            RegionTree.Block(
                entry,
                [
                    RegionTree.Block(
                        dispatch,
                        [RegionTree.Block(join, [], joinBody, null)],
                        dispatchBody,
                        join)
                ],
                entryBody,
                dispatch));
    }

    private static Instruction<IShaderValue, IShaderValue> Binary<TOperation>(
        IShaderValue result,
        IShaderValue left,
        IShaderValue right)
        where TOperation : BinaryArithmetic.IOp<TOperation> =>
        Instruction.Factory.Operation2(
            default,
            NumericBinaryArithmeticOperation<IntType<N32>, TOperation>.Instance,
            result,
            left,
            right);

    private static IEnumerable<SlangStatement> Statements(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            foreach (var nested in statement switch
            {
                SlangIf branch => Statements(branch.WhenTrue).Concat(Statements(branch.WhenFalse)),
                SlangDoOnce once => Statements(once.Body),
                SlangLoop loop => Statements(loop.Body),
                SlangScope scope => Statements(scope.Body),
                _ => []
            })
                yield return nested;
        }
    }

    private static async Task<string> CompileWgslAsync(string source)
    {
        var stem = $"cil-switch-{Guid.NewGuid():N}";
        var input = Path.Combine(Environment.CurrentDirectory, stem + ".slang");
        var startInfo = new ProcessStartInfo
        {
            FileName = "slangc",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[] { input, "-target", "wgsl" })
            startInfo.ArgumentList.Add(argument);
        try
        {
            await File.WriteAllTextAsync(input, source);
            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Failed to start slangc.");
            var standardOutput = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"slangc WGSL compilation failed with exit code {process.ExitCode}:" +
                    $"\n{standardOutput}\n{error}");
            return standardOutput;
        }
        finally
        {
            File.Delete(input);
        }
    }

    private void Capture(string name, string text)
    {
        output.WriteLine($"=== ACTUAL {name} ===");
        output.WriteLine(text);
    }

    private static int LiteralInt(IShaderValue value) =>
        Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(value).Value).Value;

    private static Label LabelNamed(RegionFunctionBody body, string name) =>
        Assert.Single(body.Labels, label => label.Name == name);

    private static FunctionDeclaration Function(string name, IShaderType result) =>
        new(name, [], new FunctionReturn(result, []), []);

    private static IShaderValue Int(int value) => ShaderValue.Literal(new I32Literal(value));
    private static IShaderValue Bool(bool value) => ShaderValue.Literal(new BoolLiteral(value));

    private static TBody ModuleBody<TBody>(ShaderModuleDeclaration<TBody> module)
        where TBody : IFunctionBody =>
        Assert.Single(module.FunctionDefinitions.Values);

    private static ShaderModuleDeclaration<RegionFunctionBody> Module(RegionFunctionBody body) =>
        new(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(
                body.Declaration,
                body));

    private static string[] ExpectedNativeTrace(
        string configuration,
        string scenario,
        int input) =>
        (configuration, scenario, input) switch
        {
            ("Debug", "effects", -1) => ["0x0", "0x33", "0x5E", "0x67", "0x82"],
            ("Debug", "effects", 0) => ["0x0", "0x35", "0x67", "0x82"],
            ("Debug", "effects", 1) => ["0x0", "0x3E", "0x67", "0x82"],
            ("Debug", "effects", 2) => ["0x0", "0x47", "0x82"],
            ("Debug", "effects", 3) => ["0x0", "0x55", "0x67", "0x82"],
            ("Debug", "loop", 0) => ["0x0", "0x4F", "0x59", "0x5E"],
            ("Debug", "loop", 4) =>
            [
                "0x0", "0x4F", "0x7", "0x26", "0x4B", "0x4F", "0x7", "0x28", "0x3D", "0x4A",
                "0x4B", "0x4F", "0x7", "0x2F", "0x3D", "0x4A", "0x4B", "0x4F", "0x7", "0x36",
                "0x3D", "0x4A", "0x4B", "0x4F", "0x59", "0x5E"
            ],
            ("Debug", "loop", 8) =>
            [
                "0x0", "0x4F", "0x7", "0x26", "0x4B", "0x4F", "0x7", "0x28", "0x3D", "0x4A",
                "0x4B", "0x4F", "0x7", "0x2F", "0x3D", "0x4A", "0x4B", "0x4F", "0x7", "0x36",
                "0x3D", "0x4A", "0x4B", "0x4F", "0x7", "0x26", "0x4B", "0x4F", "0x7", "0x28",
                "0x3D", "0x48", "0x59", "0x5E"
            ],
            ("Debug", "nested-false", 2) => ["0x0", "0x9", "0x4A"],
            ("Debug", "nested-true", 2) => ["0x0", "0xD", "0x11", "0x33", "0x42", "0x46", "0x4A"],
            ("Release", "effects", -1) => ["0x0", "0x2A", "0x52", "0x59"],
            ("Release", "effects", 0) => ["0x0", "0x2C", "0x59"],
            ("Release", "effects", 1) => ["0x0", "0x35", "0x59"],
            ("Release", "effects", 2) => ["0x0", "0x3E"],
            ("Release", "effects", 3) => ["0x0", "0x49", "0x59"],
            ("Release", "loop", 0) => ["0x0", "0x3E", "0x42"],
            ("Release", "loop", 4) =>
            [
                "0x0", "0x3E", "0x6", "0x3A", "0x3E", "0x6", "0x22", "0x35", "0x3A", "0x3E",
                "0x6", "0x29", "0x35", "0x3A", "0x3E", "0x6", "0x30", "0x35", "0x3A", "0x3E",
                "0x42"
            ],
            ("Release", "loop", 8) =>
            [
                "0x0", "0x3E", "0x6", "0x3A", "0x3E", "0x6", "0x22", "0x35", "0x3A", "0x3E",
                "0x6", "0x29", "0x35", "0x3A", "0x3E", "0x6", "0x30", "0x35", "0x3A", "0x3E",
                "0x6", "0x3A", "0x3E", "0x6", "0x22", "0x35", "0x42"
            ],
            ("Release", "nested-false", 2) => ["0x0", "0x3"],
            ("Release", "nested-true", 2) => ["0x0", "0x5", "0x27", "0x34"],
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario),
                $"No fixed trace exists for {configuration} {scenario}({input}).")
        };

    private static MethodInfo GetMethod(string name) =>
        typeof(CilSwitchTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static int DenseSwitch(int selector) =>
        selector switch
        {
            0 => 10,
            1 => 20,
            2 => 30,
            3 => 40,
            _ => 99
        };

    private sealed class NativeSwitchShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int fs([Location(0)] int selector) => DenseSwitch(selector);
    }

    private static int SwitchEffectsAndEarlyReturn(int selector)
    {
        var producerCount = 0;
        var selectedCount = 0;
        var tailCount = 0;
        var result = 0;
        producerCount = producerCount + 1;
        var selected = selector + producerCount - 1;
        switch (selected)
        {
            case 0:
                selectedCount++;
                result = 10;
                break;
            case 1:
                selectedCount++;
                result = 20;
                break;
            case 2:
                selectedCount++;
                return 30 + producerCount - 1;
            case 3:
                selectedCount++;
                result = 40;
                break;
            default:
                selectedCount++;
                result = 99;
                break;
        }
        tailCount = tailCount + 1;
        return result + producerCount * 1000 + selectedCount + tailCount * 10000;
    }

    private static int SwitchLoop(int limit)
    {
        var sum = 0;
        for (var index = 0; index < limit; index++)
        {
            switch (index & 3)
            {
                case 0:
                    continue;
                case 1:
                    sum += 10;
                    break;
                case 2:
                    sum += 20;
                    break;
                case 3:
                    sum += 30;
                    break;
            }
            if (sum > 60)
                break;
        }
        return sum;
    }

    private static int NestedConditionSwitch(bool enabled, int selector)
    {
        if (!enabled)
            return -1;
        return selector switch
        {
            0 => 10,
            1 => 20,
            2 => 30,
            3 => 40,
            _ => 99
        };
    }

    private static class Fixtures
    {
        private static readonly Type FixtureType = BuildType();

        internal static MethodInfo DuplicateTargets => Method(nameof(DuplicateTargets));
        internal static MethodInfo Empty => Method(nameof(Empty));
        internal static MethodInfo RetainedStack => Method(nameof(RetainedStack));
        internal static MethodInfo DeadMalformed => Method(nameof(DeadMalformed));
        internal static MethodInfo MissingDefault => Method(nameof(MissingDefault));
        internal static MethodInfo Underflow => Method(nameof(Underflow));
        internal static MethodInfo FloatSelector => Method(nameof(FloatSelector));
        internal static MethodInfo Int64Selector => Method(nameof(Int64Selector));
        internal static MethodInfo MergeMismatch => Method(nameof(MergeMismatch));

        internal static int Invoke(MethodInfo method, params object[] arguments) =>
            Assert.IsType<int>(method.Invoke(null, arguments));

        private static MethodInfo Method(string name) =>
            FixtureType.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} emitted fixture was not found.");

        private static Type BuildType()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("CilSwitchFixtures"),
                AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("CilSwitchFixtures");
            var type = module.DefineType(
                "CilSwitchFixtures",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            EmitDuplicate(type);
            EmitEmpty(type);
            EmitRetained(type);
            EmitDeadMalformed(type);
            EmitMissingDefault(type);
            EmitInvalidSelector(type, nameof(Underflow), null);
            EmitInvalidSelector(type, nameof(FloatSelector), OpCodes.Ldc_R4);
            EmitInvalidSelector(type, nameof(Int64Selector), OpCodes.Ldc_I8);
            EmitMergeMismatch(type);
            return type.CreateType()
                   ?? throw new InvalidOperationException("Failed to create emitted switch fixture type.");
        }

        private static void EmitDuplicate(TypeBuilder type)
        {
            var method = Define(type, nameof(DuplicateTargets), typeof(int), typeof(int));
            method.DefineParameter(1, ParameterAttributes.None, "selector");
            var il = method.GetILGenerator();
            var a = il.DefineLabel();
            var b = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Switch, [a, a, b]);
            il.MarkLabel(a);
            il.Emit(OpCodes.Ldc_I4, 99);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(b);
            il.Emit(OpCodes.Ldc_I4, 20);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitEmpty(TypeBuilder type)
        {
            var method = Define(type, nameof(Empty), typeof(int), typeof(int));
            method.DefineParameter(1, ParameterAttributes.None, "selector");
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Switch, Array.Empty<System.Reflection.Emit.Label>());
            il.Emit(OpCodes.Ldc_I4, 99);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitRetained(TypeBuilder type)
        {
            var method = Define(
                type,
                nameof(RetainedStack),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(int));
            method.DefineParameter(1, ParameterAttributes.None, "a");
            method.DefineParameter(2, ParameterAttributes.None, "b");
            method.DefineParameter(3, ParameterAttributes.None, "selector");
            var il = method.GetILGenerator();
            var a = il.DefineLabel();
            var b = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Switch, [a, b]);
            il.Emit(OpCodes.Ldc_I4, 90);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(a);
            il.Emit(OpCodes.Ldc_I4, 10);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(b);
            il.Emit(OpCodes.Ldc_I4, 20);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitDeadMalformed(TypeBuilder type)
        {
            var method = Define(type, nameof(DeadMalformed), typeof(int));
            var il = method.GetILGenerator();
            var live = il.DefineLabel();
            il.Emit(OpCodes.Br, live);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Switch, [live]);
            il.MarkLabel(live);
            il.Emit(OpCodes.Ldc_I4, 99);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitMissingDefault(TypeBuilder type)
        {
            var method = Define(type, nameof(MissingDefault), typeof(void), typeof(int));
            method.DefineParameter(1, ParameterAttributes.None, "selector");
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Switch, Array.Empty<System.Reflection.Emit.Label>());
        }

        private static void EmitInvalidSelector(TypeBuilder type, string name, OpCode? load)
        {
            var method = Define(type, name, typeof(int));
            var il = method.GetILGenerator();
            if (load == OpCodes.Ldc_R4)
                il.Emit(OpCodes.Ldc_R4, 1.0f);
            else if (load == OpCodes.Ldc_I8)
                il.Emit(OpCodes.Ldc_I8, 1L);
            il.Emit(OpCodes.Switch, Array.Empty<System.Reflection.Emit.Label>());
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitMergeMismatch(TypeBuilder type)
        {
            var method = Define(type, nameof(MergeMismatch), typeof(void), typeof(bool), typeof(int));
            method.DefineParameter(1, ParameterAttributes.None, "ordinary");
            method.DefineParameter(2, ParameterAttributes.None, "selector");
            var il = method.GetILGenerator();
            var switchPath = il.DefineLabel();
            var join = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Brtrue, switchPath);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Br, join);
            il.MarkLabel(switchPath);
            il.Emit(OpCodes.Ldc_R4, 1.0f);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Switch, [join]);
            il.MarkLabel(join);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        }

        private static MethodBuilder Define(
            TypeBuilder type,
            string name,
            Type returnType,
            params Type[] parameterTypes) =>
            type.DefineMethod(
                name,
                MethodAttributes.Public | MethodAttributes.Static,
                returnType,
                parameterTypes);
    }
}
