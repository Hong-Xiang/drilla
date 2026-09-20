using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Mathematics;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CooperationAdmissionTests(ITestOutputHelper output)
{
    [Fact]
    public void MaximalAndInvalidProfilesRejectAtConstruction()
    {
        Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(
                CLSLCompileTarget.IR,
                CLSLCooperationProfile.MaximalReconvergence)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CLSLCompiler(new(
                CLSLCompileTarget.IR,
                (CLSLCooperationProfile)99)));
    }

    [Fact]
    public void ScalarRejectsKnownDerivativeAcrossEveryPublicRoute()
    {
        AssertAllPublicRoutesReject(new DirectDerivativeShader(), "DerivativeQuad");
        AssertAllPublicRoutesReject(new TwoHopDerivativeShader(), "DerivativeQuad");
        AssertAllPublicRoutesReject(new VaryingConditionalDerivativeShader(), "DerivativeQuad");
    }

    private static void AssertAllPublicRoutesReject(ISharpShader shader, string expected)
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);

        var compileShader = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(shader));
        var compileRaw = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw));
        Assert.Contains(expected, compileShader.Message);
        Assert.Contains("Scalar", compileRaw.Message);
        Assert.Contains("stage 'Fragment'", compileRaw.Message);
        foreach (var target in Enum.GetValues<CLSLCompileTarget>())
        {
            var error = Assert.Throws<NotSupportedException>(() =>
                new CLSLCompiler(new(target)).Emit(shader));
            Assert.Contains(expected, error.Message);
            Assert.Contains("IL_", error.Message);
        }
    }

    [Fact]
    public void PortableDirectDerivativeCompilesThroughIrSlangAndWgsl()
    {
        var shader = new DirectDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var compiler = new CLSLCompiler(new(
            CLSLCompileTarget.IR,
            CLSLCooperationProfile.PortableWgsl));

        var fromShader = compiler.Compile(shader);
        var fromRaw = compiler.Compile(raw);
        var ir = compiler.Emit(shader);
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine("=== C2 direct source ===");
        output.WriteLine(
            "[Fragment] [return: Location(0)] static float Fragment([Location(0)] float varying) => DMath.dpdx(varying);");
        output.WriteLine("=== C2 direct CIL/IR ===");
        output.WriteLine(ir);
        output.WriteLine("=== C2 direct Slang ===");
        output.WriteLine(slang);
        output.WriteLine("=== C2 direct WGSL ===");
        output.WriteLine(wgsl);

        Assert.Single(fromShader.FunctionDefinitions);
        Assert.Single(fromRaw.FunctionDefinitions);
        Assert.Contains("ddx(", slang);
        Assert.DoesNotContain("dpdx(", slang);
        Assert.Contains("dpdx(", wgsl);
        Assert.Contains("@fragment", wgsl);
    }

    [Fact]
    public void PortableVectorDerivativeCompilesToVerifiedTargetSpelling()
    {
        var slang = Emit(new VectorDerivativeShader(), CLSLCompileTarget.SLang);
        var wgsl = Emit(new VectorDerivativeShader(), CLSLCompileTarget.WGSL);
        var fwidth = Emit(new FwidthDerivativeShader(), CLSLCompileTarget.WGSL);

        Assert.Contains("ddy(", slang);
        Assert.Contains("dpdy(", wgsl);
        Assert.Contains("fwidth(", fwidth);
    }

    [Theory]
    [InlineData(typeof(DdxCollisionShader), "ddx")]
    [InlineData(typeof(DdyCollisionShader), "ddy")]
    [InlineData(typeof(FwidthCollisionShader), "fwidth")]
    public void UsedDerivativeTargetNameCollisionRejectsRealCilWgsl(Type shaderType, string targetName)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);

        var error = Assert.Throws<NotSupportedException>(() =>
            Emit(shader, CLSLCompileTarget.WGSL));

        output.WriteLine($"=== {targetName} collision raw CIL ===");
        output.WriteLine(Assert.Single(
            raw.FunctionDefinitions,
            item => item.Key.Name == "Fragment").Value.PrettyPrint());
        output.WriteLine(error.Message);
        Assert.Contains($"mapped target spelling '{targetName}'", error.Message);
        Assert.Contains($"module declaration '{targetName}'", error.Message);
        Assert.Contains("DerivativeQuad", error.Message);
    }

    [Theory]
    [InlineData(typeof(DdxCollisionShader), "ddx")]
    [InlineData(typeof(DdyCollisionShader), "ddy")]
    [InlineData(typeof(FwidthCollisionShader), "fwidth")]
    public void DirectTargetLoweringRejectsUsedDerivativeTargetNameCollision(
        Type shaderType,
        string targetName)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var error = Assert.Throws<NotSupportedException>(() => DirectTarget(shader));

        output.WriteLine(error.Message);
        Assert.Contains("Slang target lowering cannot map registered derivative", error.Message);
        Assert.Contains($"'{targetName}'", error.Message);
        Assert.Contains($"module declaration '{targetName}'", error.Message);
    }

    [Fact]
    public async Task DirectTargetLoweringPreservesNoncollidingBuiltinsAndOrdinaryHelpers()
    {
        var derivativeSlang = new SlangEmitter(DirectTarget(new DirectDerivativeShader())).Emit();
        var derivativeWgsl = await new SlangService().CompileToWgslAsync(derivativeSlang);
        var helperSlang = new SlangEmitter(DirectTarget(new SourceDpdxHelperShader())).Emit();
        var helperWgsl = await new SlangService().CompileToWgslAsync(helperSlang);
        var unusedTargetNameSlang = new SlangEmitter(DirectTarget(new UnusedDdxHelperShader())).Emit();
        var unusedTargetNameWgsl = await new SlangService().CompileToWgslAsync(unusedTargetNameSlang);

        Assert.Contains("ddx(", derivativeSlang);
        Assert.Contains("dpdx(", derivativeWgsl);
        Assert.Contains("dpdx(", helperSlang);
        Assert.Contains("fn dpdx_", helperWgsl);
        Assert.Contains("ddx(", unusedTargetNameSlang);
        Assert.Contains("fn ddx_", unusedTargetNameWgsl);
    }

    [Fact]
    public void SameSourceNameDpdxHelperRemainsOrdinary()
    {
        var shader = new SourceDpdxHelperShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var normalized = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var entry = Assert.Single(
            normalized.FunctionDefinitions.Keys,
            function => function.Name == nameof(SourceDpdxHelperShader.Fragment));

        var summary = FunctionEffectAnalysis.Analyze(normalized)[entry];
        var facts = CLSLCooperationAnalysis.Analyze(normalized);
        var compiled = new CLSLCompiler(new(
            CLSLCompileTarget.WGSL,
            CLSLCooperationProfile.PortableWgsl)).Compile(shader);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        Assert.DoesNotContain(
            summary.RequirementSites,
            site => (site.Requirements & OperationRequirement.DerivativeQuad) != 0);
        Assert.Empty(facts.EntryUniformQuadParticipations);
        Assert.Equal(2, compiled.FunctionDefinitions.Count);
        Assert.Contains("fn dpdx_", wgsl);
    }

    [Fact]
    public void PortableTwoHopHelperPublishesOriginalParticipationFacts()
    {
        var shader = new TwoHopDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var normalized = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());

        var facts = CLSLCooperationAnalysis.Analyze(normalized);
        var participation = Assert.Single(facts.EntryUniformQuadParticipations);
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine("=== C2 helper effects/participation ===");
        output.WriteLine($"entry={participation.Entry.Name}");
        foreach (var (function, labels) in participation.OriginalBlocks)
            output.WriteLine($"{function.Name}: {string.Join(" -> ", labels.Select(label => label.Name))}");
        foreach (var site in participation.OriginalSensitiveSites)
            output.WriteLine(
                $"sensitive {site.Function.Name}/{site.Label.Name}/{site.InstructionOrdinal}: {site.Requirements}");
        foreach (var call in participation.CallInheritance)
            output.WriteLine(
                $"call {call.Caller.Name}/{call.Label.Name}/{call.InstructionOrdinal} -> {call.Callee.Name}");
        output.WriteLine("=== C2 helper Slang ===");
        output.WriteLine(slang);
        output.WriteLine("=== C2 helper WGSL ===");
        output.WriteLine(wgsl);

        Assert.Equal(nameof(TwoHopDerivativeShader.Fragment), participation.Entry.Name);
        Assert.Equal(3, participation.OriginalBlocks.Count);
        Assert.Single(participation.OriginalSensitiveSites);
        Assert.Equal(2, participation.CallInheritance.Length);
        Assert.Contains("ddx(", slang);
        Assert.Contains("dpdx(", wgsl);
    }

    [Theory]
    [InlineData(typeof(VaryingConditionalDerivativeShader), "conditional control")]
    [InlineData(typeof(VaryingConditionalHelperDerivativeShader), "conditional control")]
    [InlineData(typeof(VertexDerivativeShader), "not an unambiguous fragment")]
    [InlineData(typeof(ComputeDerivativeShader), "not an unambiguous fragment")]
    [InlineData(typeof(SharedStageDerivativeShader), "not an unambiguous fragment")]
    [InlineData(typeof(NonlinearPureHelperShader), "conditional control")]
    [InlineData(typeof(SwitchDerivativeShader), "control is not admitted")]
    [InlineData(typeof(LoopContinueDerivativeShader), "RegionKind.Loop")]
    [InlineData(typeof(RecursiveDerivativeShader), "complete effect summaries")]
    [InlineData(typeof(CoarseDerivativeShader), "no verified spelling")]
    [InlineData(typeof(HalfDerivativeShader), "f32 scalar/vector")]
    [InlineData(typeof(DoubleDerivativeShader), "f32 scalar/vector")]
    public void PortableRejectsUnsupportedStageShapeAndOperation(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var error = Assert.Throws<NotSupportedException>(() =>
            Emit(shader, CLSLCompileTarget.IR));

        output.WriteLine(error.Message);
        Assert.Contains(expected, error.Message);
        Assert.Contains("PortableWgsl", error.Message);
    }

    [Fact]
    public void PortableRejectsUnownedAndProviderOnlyCooperation()
    {
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var unowned = DerivativeCallModule(
            new CallOperation((FunctionType)dpdx.Type),
            ShaderValue.Intermediate(ShaderType.F32),
            dpdx,
            [ShaderValue.Intermediate(ShaderType.F32)],
            []);
        var provider = ManualModule(
            "Provider",
            [new FragmentAttribute()],
            new ProviderOperation(OperationRequirement.SubgroupParticipation));
        var derivativeProvider = ManualModule(
            "DerivativeProvider",
            [new FragmentAttribute()],
            new ProviderOperation(OperationRequirement.DerivativeQuad));

        var unownedError = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(unowned));
        var providerError = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(provider));
        var derivativeProviderError = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(derivativeProvider));

        Assert.Contains("no reachable shader-stage entry", unownedError.Message);
        Assert.Contains("SubgroupParticipation", providerError.Message);
        Assert.Contains("implements derivative-quad participation only", providerError.Message);
        Assert.Contains("not a supported builtin call", derivativeProviderError.Message);
    }

    [Fact]
    public void ScalarRejectsKnownSensitiveRequirementEvenWhenSummaryIsIncomplete()
    {
        var module = ManualModule(
            "KnownAndUnknown",
            [new FragmentAttribute()],
            new ProviderOperation(OperationRequirement.DerivativeQuad),
            new UnknownOperation());

        var error = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(module, CLSLCooperationProfile.Scalar));
        var portableError = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(module));

        Assert.Contains("DerivativeQuad", error.Message);
        Assert.Contains("Scalar", error.Message);
        Assert.Contains("complete effect summaries", portableError.Message);
    }

    [Fact]
    public void PortableRejectsMalformedDerivativeCallsAtNormalizedBoundary()
    {
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var f32 = ShaderValue.Intermediate(ShaderType.F32);
        var i32 = ShaderValue.Intermediate(ShaderType.I32);
        var validOperation = new CallOperation((FunctionType)dpdx.Type);
        var cases = new[]
        {
            (
                Name: "missing result",
                Module: DerivativeCallModule(validOperation, null, dpdx, [f32]),
                Expected: "missing result"),
            (
                Name: "wrong result",
                Module: DerivativeCallModule(validOperation, i32, dpdx, [f32]),
                Expected: "requires result 'f32', got 'i32'"),
            (
                Name: "operation signature",
                Module: DerivativeCallModule(
                    new CallOperation(new FunctionType([ShaderType.I32], ShaderType.F32)),
                    f32,
                    dpdx,
                    [f32]),
                Expected: "call operation signature"),
            (
                Name: "zero arguments",
                Module: DerivativeCallModule(validOperation, f32, dpdx, []),
                Expected: "requires 1 arguments, got 0"),
            (
                Name: "extra arguments",
                Module: DerivativeCallModule(validOperation, f32, dpdx, [f32, f32]),
                Expected: "requires 1 arguments, got 2"),
            (
                Name: "bad argument type",
                Module: DerivativeCallModule(validOperation, f32, dpdx, [i32]),
                Expected: "requires 'f32', got 'i32'")
        };

        foreach (var item in cases)
        {
            var error = Assert.Throws<NotSupportedException>(() =>
                CLSLCooperationAnalysis.Analyze(item.Module));
            output.WriteLine($"{item.Name}: {error.Message}");
            Assert.Contains(item.Expected, error.Message);
            Assert.Contains("DerivativeQuad", error.Message);
        }
    }

    [Fact]
    public void PortableRejectsMalformedOrdinaryHelperCallInCooperativeClosure()
    {
        var module = MalformedHelperCallModule();

        var error = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(module));

        Assert.Contains("call argument 0", error.Message);
        Assert.Contains("requires 'f32', got 'i32'", error.Message);
        Assert.Contains("entry 'Fragment'", error.Message);
    }

    [Fact]
    public void PortableAdmitsStraightLinePointerTransferAndPreservesOriginalLabels()
    {
        var parameter = new ParameterDeclaration("varying", ShaderType.F32, [new LocationAttribute(0)]);
        var declaration = new FunctionDeclaration(
            "PointerChain",
            [parameter],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("z-entry");
        var returned = Label.Create("a-return");
        var pointer = ShaderValue.Intermediate(parameter.Value.Type);
        var loaded = ShaderValue.Intermediate(ShaderType.F32);
        var derivative = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                new(returned, [parameter.Value])));
        var returnBody = RegionFixture.Body(
            returned,
            [pointer],
            [
                Instruction.Factory.Load(default, new LoadOperation(), loaded, pointer),
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivative,
                    [dpdx, loaded],
                    "pointer-derivative")
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivative));
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(returned, [], returnBody, null)],
                entryBody,
                returned));
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));

        var facts = CLSLCooperationAnalysis.Analyze(module);
        var labels = Assert.Single(facts.EntryUniformQuadParticipations).OriginalBlocks[declaration];

        Assert.Equal(2, labels.Length);
        Assert.Same(entry, labels[0]);
        Assert.Same(returned, labels[1]);
    }

    [Fact]
    public void UnrelatedScalarVertexEntryDoesNotContaminateFragmentDerivative()
    {
        var wgsl = Emit(new IndependentStagesShader(), CLSLCompileTarget.WGSL);

        Assert.Contains("@vertex", wgsl);
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void OriginalDeclarationBodyMismatchRejectsBeforeCompilationPasses()
    {
        var shader = new DirectDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var malformed = raw with { Declarations = [] };
        var original = Assert.Single(raw.FunctionDefinitions);
        var replacement = new FunctionDeclaration(
            original.Key.Name,
            original.Key.Parameters,
            original.Key.Return,
            original.Key.Attributes);
        var mismatched = new ShaderModuleDeclaration<RawCilFunctionBody>(
            [replacement],
            ImmutableDictionary<FunctionDeclaration, RawCilFunctionBody>.Empty.Add(
                replacement,
                original.Value));

        var error = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(
                CLSLCompileTarget.IR,
                CLSLCooperationProfile.PortableWgsl)).Compile(malformed));
        var mismatchError = Assert.Throws<NotSupportedException>(() =>
            new CLSLCompiler(new(
                CLSLCompileTarget.IR,
                CLSLCooperationProfile.PortableWgsl)).Compile(mismatched));

        Assert.Contains("without its original declaration", error.Message);
        Assert.Contains("body/declaration mismatch", mismatchError.Message);
    }

    [Fact]
    public void DefaultScalarOnlyShaderRemainsUnrestricted()
    {
        var shader = new ConditionalScalarShader();

        var ir = new CLSLCompiler(new(CLSLCompileTarget.IR)).Emit(shader);

        Assert.Contains(nameof(ConditionalScalarShader.Fragment), ir);
    }

    [Fact]
    public void ScalarCompileDoesNotInvokeSelectedEmissionBackend()
    {
        var shader = new UInt32NegationShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);

        foreach (var target in new[] { CLSLCompileTarget.SLang, CLSLCompileTarget.WGSL })
        {
            var compiler = new CLSLCompiler(new(target));
            Assert.Single(compiler.Compile(shader).FunctionDefinitions);
            Assert.Single(compiler.Compile(raw).FunctionDefinitions);
            Assert.Throws<NotSupportedException>(() => compiler.Emit(shader));
        }
    }

    private static string Emit(ISharpShader shader, CLSLCompileTarget target) =>
        new CLSLCompiler(new(target, CLSLCooperationProfile.PortableWgsl)).Emit(shader);

    private static ShaderModuleDeclaration<SlangFunctionBody> DirectTarget(ISharpShader shader)
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var normalized = CilModuleCompiler.Compile(raw)
            .RunPass(new FunctionToOperationPass())
            .RunPass(new StablePointerRegionParameterPass());
        return new SlangTargetLowering().Lower(normalized);
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> ManualModule(
        string name,
        ImmutableHashSet<IShaderAttribute> attributes,
        params IOperation[] operations)
    {
        var declaration = new FunctionDeclaration(
            name,
            [],
            new FunctionReturn(ShaderType.Unit, []),
            attributes);
        var label = Label.Create("entry");
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                label,
                [],
                RegionFixture.Body(
                    label,
                    [],
                    operations.Select((operation, index) =>
                        Instruction<IShaderValue, IShaderValue>.Create(
                            operation,
                            null,
                            [],
                            $"manual-{index}")),
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> DerivativeCallModule(
        CallOperation operation,
        IShaderValue? result,
        FunctionDeclaration callee,
        IReadOnlyList<IShaderValue> arguments,
        ImmutableHashSet<IShaderAttribute>? attributes = null)
    {
        var declaration = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            attributes ?? [new FragmentAttribute()]);
        var label = Label.Create("entry");
        var call = Instruction<IShaderValue, IShaderValue>.Create(
            operation,
            result,
            [callee, .. arguments],
            "malformed-derivative");
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                label,
                [],
                RegionFixture.Body(
                    label,
                    [],
                    [call],
                    Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(
                        ShaderValue.Literal(new F32Literal(0.0f)))),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> MalformedHelperCallModule()
    {
        var helperParameter = new ParameterDeclaration("value", ShaderType.F32, []);
        var helper = new FunctionDeclaration(
            "Helper",
            [helperParameter],
            new FunctionReturn(ShaderType.F32, []),
            []);
        var entry = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var helperLabel = Label.Create("helper");
        var entryLabel = Label.Create("entry");
        var loaded = ShaderValue.Intermediate(ShaderType.F32);
        var derivative = ShaderValue.Intermediate(ShaderType.F32);
        var helperResult = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var helperBody = RegionFixture.CreateFunctionBody(
            helper,
            RegionTree.Block(
                helperLabel,
                [],
                RegionFixture.Body(
                    helperLabel,
                    [],
                    [
                        Instruction.Factory.Load(
                            default,
                            new LoadOperation(),
                            loaded,
                            helperParameter.Value),
                        Instruction<IShaderValue, IShaderValue>.Create(
                            new CallOperation((FunctionType)dpdx.Type),
                            derivative,
                            [dpdx, loaded],
                            "helper-derivative")
                    ],
                    Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivative)),
                null));
        var entryBody = RegionFixture.CreateFunctionBody(
            entry,
            RegionTree.Block(
                entryLabel,
                [],
                RegionFixture.Body(
                    entryLabel,
                    [],
                    [
                        Instruction<IShaderValue, IShaderValue>.Create(
                            new CallOperation((FunctionType)helper.Type),
                            helperResult,
                            [helper, ShaderValue.Intermediate(ShaderType.I32)],
                            "malformed-helper")
                    ],
                    Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(helperResult)),
                null));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [entry, helper],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty
                .Add(entry, entryBody)
                .Add(helper, helperBody));
    }

    private sealed class DirectDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => DMath.dpdx(varying);
    }

    private sealed class TwoHopDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => First(varying);

        [ShaderMethod]
        private static float First(float value) => Second(value);

        [ShaderMethod]
        private static float Second(float value) => DMath.dpdx(value);
    }

    private sealed class VectorDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static vec2f32 Fragment([Location(0)] vec2f32 varying) => DMath.dpdy(varying);
    }

    private sealed class FwidthDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => DMath.fwidth(varying);
    }

    private sealed class VaryingConditionalDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (varying > 0.0f)
                return DMath.dpdx(varying);
            return varying;
        }
    }

    private sealed class VaryingConditionalHelperDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (varying > 0.0f)
                return Derivative(varying);
            return varying;
        }

        [ShaderMethod]
        private static float Derivative(float value) => DMath.dpdx(value);
    }

    private sealed class VertexDerivativeShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 Vertex([Location(0)] float varying) =>
            DMath.vec4(DMath.dpdx(varying), 0.0f, 0.0f, 1.0f);
    }

    private sealed class ComputeDerivativeShader : ISharpShader
    {
        [Compute]
        public static float Compute(float varying) => DMath.dpdx(varying);
    }

    private sealed class SharedStageDerivativeShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 Vertex([Location(0)] float varying) =>
            DMath.vec4(Shared(varying), 0.0f, 0.0f, 1.0f);

        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => Shared(varying);

        [ShaderMethod]
        private static float Shared(float value) => DMath.dpdx(value);
    }

    private sealed class NonlinearPureHelperShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.dpdx(Choose(varying));

        [ShaderMethod]
        private static float Choose(float value) => value > 0.0f ? value : -value;
    }

    private sealed class SwitchDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment(
            [Location(0)] float varying,
            [Location(1)] int selector) =>
            DMath.dpdx(selector switch
            {
                0 => varying,
                1 => varying + 1.0f,
                2 => varying + 2.0f,
                _ => varying + 3.0f
            });
    }

    private sealed class LoopContinueDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            for (var index = 0; index < 2; index++)
            {
                if (varying > 0.0f)
                    continue;
                varying += 1.0f;
            }
            return DMath.dpdx(varying);
        }
    }

    private sealed class RecursiveDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.dpdx(Recurse(varying));

        [ShaderMethod]
        private static float Recurse(float value) => value == 0.0f ? value : Recurse(value - 1.0f);
    }

    private sealed class CoarseDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => DMath.dpdxCoarse(varying);
    }

    private sealed class DoubleDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static double Fragment([Location(0)] double varying) => DMath.dpdx(varying);
    }

    private sealed class HalfDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static Half Fragment([Location(0)] Half varying) => DMath.dpdx(varying);
    }

    private sealed class IndependentStagesShader : ISharpShader
    {
        [Vertex]
        [return: Builtin(BuiltinBinding.position)]
        public static vec4f32 Vertex() => DMath.vec4(0.0f, 0.0f, 0.0f, 1.0f);

        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => DMath.dpdx(varying);
    }

    private sealed class ConditionalScalarShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            varying > 0.0f ? varying : -varying;
    }

    private sealed class UInt32NegationShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static long Fragment([Location(0)] uint value) => -value;
    }

    private sealed class DdxCollisionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.dpdx(varying) + ddx(varying);

        [ShaderMethod]
        private static float ddx(float value) => value + 42.0f;
    }

    private sealed class DdyCollisionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.dpdy(varying) + ddy(varying);

        [ShaderMethod]
        private static float ddy(float value) => value + 42.0f;
    }

    private sealed class FwidthCollisionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.fwidth(varying) + fwidth(varying);

        [ShaderMethod]
        private static float fwidth(float value) => value + 42.0f;
    }

    private sealed class SourceDpdxHelperShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => dpdx(varying);

        [ShaderMethod]
        private static float dpdx(float value) => value + 42.0f;
    }

    private sealed class UnusedDdxHelperShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) => ddx(varying);

        [ShaderMethod]
        private static float ddx(float value) => value + 42.0f;
    }

    private sealed class ProviderOperation(OperationRequirement requirements)
        : UnknownOperation, IOperationRequirementProvider
    {
        public OperationRequirement Requirements { get; } = requirements;
        public override string Name => "provider";
    }

    private class UnknownOperation : IOperation
    {
        public FunctionDeclaration Function => throw new NotSupportedException();
        public virtual string Name => "unknown";
        public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotSupportedException();

        public TResult EvaluateInstruction<TValue, TResultValue, TSemantic, TResult>(
            Instruction<TValue, TResultValue> instruction,
            TSemantic semantic)
            where TSemantic : IOperationSemantic<
                Instruction<TValue, TResultValue>,
                TValue,
                TResultValue,
                TResult> =>
            throw new InvalidOperationException("Provider operations are analysis-only fixtures.");
    }
}
