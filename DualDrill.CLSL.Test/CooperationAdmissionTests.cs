using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
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
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
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
    public void PortableUniformHelperConditionalRetainsRealCilAndCompiles()
    {
        var shader = new UniformConditionalDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var fragment = Assert.Single(
            raw.FunctionDefinitions,
            item => item.Key.Name == nameof(UniformConditionalDerivativeShader.Fragment));
        var instructions = fragment.Value.Code.Instructions;

        Assert.Contains(
            instructions,
            item => item.Instruction.OpCode.Name?.StartsWith("brtrue", StringComparison.Ordinal) is true ||
                    item.Instruction.OpCode.Name?.StartsWith("brfalse", StringComparison.Ordinal) is true);
        Assert.Contains(
            instructions,
            item => item.Instruction.Operand is System.Reflection.MethodInfo
            {
                Name: nameof(UniformConditionalDerivativeShader.UniformChoice)
            });

        var normalized = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        foreach (var body in normalized.FunctionDefinitions.Values)
            output.WriteLine(body.Dump());
        var participation = Assert.Single(
            CLSLCooperationAnalysis.Analyze(normalized).EntryUniformQuadParticipations);
        var fragmentDeclaration = Assert.Single(
            normalized.FunctionDefinitions.Keys,
            function => function.Name == nameof(UniformConditionalDerivativeShader.Fragment));
        var helperDeclaration = Assert.Single(
            normalized.FunctionDefinitions.Keys,
            function => function.Name == nameof(UniformConditionalDerivativeShader.UniformChoice));
        var fragmentFacts = participation.Uniformity[fragmentDeclaration];
        var helperFacts = participation.Uniformity[helperDeclaration];
        var pointer = normalized.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        CooperationAdmission.CheckTargetCorrespondence(pointer, target, new([participation]));
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine("=== C3 uniform conditional CIL ===");
        output.WriteLine(fragment.Value.PrettyPrint());
        output.WriteLine("=== C3 uniformity facts ===");
        output.WriteLine(
            $"fragment conditionals={fragmentFacts.UniformConditionals.Length}, " +
            $"helper returns-uniform={helperFacts.ReturnsUniform}");
        output.WriteLine("=== C3 verified target AST ===");
        output.WriteLine(target.GetBody(fragmentDeclaration).PrettyPrint());
        output.WriteLine("=== C3 uniform conditional Slang ===");
        output.WriteLine(slang);
        output.WriteLine("=== C3 uniform conditional WGSL ===");
        output.WriteLine(wgsl);

        Assert.Single(fragmentFacts.UniformConditionals);
        Assert.True(helperFacts.ReturnsUniform);
        Assert.Contains(nameof(UniformConditionalDerivativeShader.UniformChoice), slang);
        Assert.Contains("if", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void ContextIndependentLiteralHelperIgnoresVaryingArgument()
    {
        var facts = Analyze(new UniformIgnoringArgumentShader());
        var helper = Assert.Single(
            facts.EntryUniformQuadParticipations[0].Uniformity.Values,
            item => item.Function.Name == nameof(UniformIgnoringArgumentShader.Always));

        Assert.True(helper.ReturnsUniform);
        Assert.Contains("dpdx(", Emit(new UniformIgnoringArgumentShader(), CLSLCompileTarget.WGSL));
    }

    [Fact]
    public void UniformBuiltinTargetSpellingCollisionRejects()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            Emit(new UniformBuiltinCollisionShader(), CLSLCompileTarget.WGSL));

        Assert.Contains("numeric builtin 'sin'", error.Message);
        Assert.Contains("target spelling 'sin'", error.Message);
        Assert.Contains("module declaration 'sin'", error.Message);
    }

    [Fact]
    public void ExactIncomingArmsProveDifferentUniformPhiValues()
    {
        var facts = CLSLCooperationAnalysis.Analyze(PhiConditionalModule());
        var participation = Assert.Single(facts.EntryUniformQuadParticipations);
        var function = Assert.Single(participation.Uniformity.Values);

        Assert.Equal(2, function.UniformConditionals.Length);
        Assert.Equal(2, function.UniformBindings.Count(binding => binding.ParameterPosition == 0));
        Assert.Contains(
            function.UniformValues,
            fact => fact.Kind is CooperationUniformValueKind.BlockParameter);
    }

    [Fact]
    public void SameTargetConditionalArmsRemainDistinctAndParallel()
    {
        var module = PhiConditionalModule(sameTarget: true, parallel: true);
        var facts = CLSLCooperationAnalysis.Analyze(module);
        var participation = Assert.Single(facts.EntryUniformQuadParticipations);
        var function = Assert.Single(participation.Uniformity.Values);
        var entry = function.OriginalBlocks[0];
        var bindings = function.UniformBindings.Where(binding =>
            ReferenceEquals(binding.Source, entry)).ToArray();

        Assert.Equal(4, bindings.Length);
        Assert.Equal([0, 0, 1, 1], bindings.Select(static binding => binding.Arm).Order());
        Assert.Equal([0, 0, 1, 1], bindings.Select(static binding => binding.ParameterPosition).Order());
    }

    [Fact]
    public void OneVaryingIncomingPhiArmRejects()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(PhiConditionalModule(varyingIncoming: true)));

        Assert.Contains("conditional control is varying", error.Message);
    }

    [Fact]
    public void ProviderNoneCannotProveUniformControl()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(ProviderConditionalModule()));

        Assert.Contains("conditional control is varying", error.Message);
    }

    [Fact]
    public void UniformFactsIgnoreLabelNamesAndRegionStorageOrder()
    {
        var left = Assert.Single(
            CLSLCooperationAnalysis.Analyze(PhiConditionalModule(prefix: "left-", reverseBindings: false))
                .EntryUniformQuadParticipations).Uniformity.Values.Single();
        var right = Assert.Single(
            CLSLCooperationAnalysis.Analyze(PhiConditionalModule(prefix: "right-", reverseBindings: true))
                .EntryUniformQuadParticipations).Uniformity.Values.Single();

        Assert.Equal(left.OriginalBlocks.Length, right.OriginalBlocks.Length);
        Assert.Equal(left.UniformValues.Length, right.UniformValues.Length);
        Assert.Equal(left.UniformBindings.Length, right.UniformBindings.Length);
        Assert.Equal(left.UniformConditionals.Length, right.UniformConditionals.Length);
    }

    [Fact]
    public void ProductionTargetVerifierRejectsCorruptedConditionalOriginsAndControlData()
    {
        var prepared = PrepareTarget(PhiConditionalModule(sameTarget: true, parallel: true));
        var body = prepared.Target.GetBody(prepared.Function);
        var origins = body.Origins;
        var transfer = origins.Transfers[0];
        var gate = origins.Gates[0];
        var sourceConditional = origins.Conditionals[0];
        var uniformDefinition = origins.Definitions.First(origin =>
            origin.Source.Operation is LiteralOperation);
        var cases = new (string Name, Func<SlangFunctionBody> Mutate, string Expected)[]
        {
            (
                "delete-token-assignment",
                () => Rewrite(body, statement =>
                    ReferenceEquals(statement, transfer.TokenAssignment) ? null : statement),
                "origin references a target statement"),
            (
                "move-token-after-break",
                () => Rewrite(
                    body,
                    static statement => statement,
                    statements => MoveAfter(
                        statements,
                        transfer.TokenAssignment,
                        transfer.Break)),
                "snapshot all arguments before slot writes, token write, and break"),
            (
                "comparison-before-definition",
                () =>
                {
                    var without = Rewrite(
                        body,
                        statement => ReferenceEquals(statement, gate.Comparison) ? null : statement);
                    return new SlangFunctionBody(
                        body.Declaration,
                        new SlangBlock([gate.Comparison, .. without.Body.Statements]),
                        without.Origins);
                },
                "token comparison does not immediately precede"),
            (
                "swapped-arms",
                () => Rewrite(
                    body,
                    statement => ReferenceEquals(statement, sourceConditional.Conditional)
                        ? sourceConditional.Conditional with
                        {
                            WhenTrue = sourceConditional.Conditional.WhenFalse,
                            WhenFalse = sourceConditional.Conditional.WhenTrue
                        }
                        : statement),
                "conditional arm 0 contains the wrong source transfer"),
            (
                "wrong-carrier",
                () => Rewrite(
                    body,
                    static statement => statement,
                    statements => WrapTransferInDoOnce(statements, transfer)),
                "not owned by its source-label carrier"),
            (
                "wrong-continuation-owner",
                () => new SlangFunctionBody(
                    body.Declaration,
                    body.Body,
                    body.Origins with
                    {
                        Gates =
                        [
                            body.Origins.Gates[0] with
                            {
                                Continuation = body.Origins.Gates[0].Continuation with
                                {
                                    Owner = Label.Create("wrong-owner")
                                }
                            },
                            .. body.Origins.Gates[1..]
                        ]
                    }),
                "token gate does not compare the correct token"),
            (
                "missing-slot-assignment",
                () => Rewrite(
                    body,
                    statement => ReferenceEquals(statement, transfer.Arguments[0].Assignment)
                        ? null
                        : statement),
                "origin references a target statement"),
            (
                "changed-uniform-producer",
                () =>
                {
                    var changed = new SlangBind(
                        Instruction<SlangOperand, IShaderValue>.Create(
                            uniformDefinition.Definition.Instruction.Operation,
                            uniformDefinition.Definition.Instruction.Result,
                            [new SlangValueOperand(ShaderValue.Literal(new BoolLiteral(false)))],
                            uniformDefinition.Definition.Instruction.Payload));
                    return Rewrite(
                        body,
                        statement => ReferenceEquals(statement, uniformDefinition.Definition)
                            ? changed
                            : statement);
                },
                "does not preserve its operation/result/operand lineage"),
            (
                "extra-control-use",
                () => new SlangFunctionBody(
                    body.Declaration,
                    new SlangBlock(
                    [
                        new SlangIf(
                            new SlangValueOperand(ShaderValue.Literal(new BoolLiteral(true))),
                            SlangBlock.Empty,
                            SlangBlock.Empty),
                        .. body.Body.Statements
                    ]),
                    body.Origins),
                "unaccounted conditional")
        };

        foreach (var item in cases)
        {
            var definitions = prepared.Target.FunctionDefinitions.SetItem(
                prepared.Function,
                item.Mutate());
            var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
                prepared.Target.Declarations,
                definitions);
            var error = Assert.Throws<NotSupportedException>(() =>
                CooperationAdmission.CheckTargetCorrespondence(
                    prepared.Pointer,
                    corrupted,
                    prepared.Facts));
            output.WriteLine($"{item.Name}: {error.Message}");
            Assert.Contains(item.Expected, error.Message);
        }
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
    [InlineData(typeof(IdentityLiteralConditionalShader), "conditional control")]
    [InlineData(typeof(DerivativeConditionShader), "conditional control")]
    [InlineData(typeof(SwitchDerivativeShader), "control is not admitted")]
    [InlineData(typeof(UniformSwitchDerivativeShader), "switch control is not admitted")]
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

    private static CLSLCooperationFacts Analyze(ISharpShader shader)
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        return CLSLCooperationAnalysis.Analyze(
            CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass()));
    }

    private static PreparedTarget PrepareTarget(ShaderModuleDeclaration<RegionFunctionBody> source)
    {
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        return new(pointer, target, facts, Assert.Single(source.FunctionDefinitions.Keys));
    }

    private static SlangFunctionBody Rewrite(
        SlangFunctionBody source,
        Func<SlangStatement, SlangStatement?> rewrite,
        Func<ImmutableArray<SlangStatement>, ImmutableArray<SlangStatement>>? rewriteBlock = null)
    {
        var replacements = new Dictionary<SlangStatement, SlangStatement>(
            ReferenceEqualityComparer.Instance);

        SlangBlock Visit(SlangBlock block)
        {
            var statements = ImmutableArray.CreateBuilder<SlangStatement>();
            foreach (var original in block.Statements)
            {
                var nested = original switch
                {
                    SlangScope scope => scope with { Body = Visit(scope.Body) },
                    SlangIf conditional => conditional with
                    {
                        WhenTrue = Visit(conditional.WhenTrue),
                        WhenFalse = Visit(conditional.WhenFalse)
                    },
                    SlangDoOnce once => once with { Body = Visit(once.Body) },
                    SlangLoop loop => loop with { Body = Visit(loop.Body) },
                    _ => original
                };
                var requested = rewrite(original);
                var changed = requested is null
                    ? null
                    : ReferenceEquals(requested, original) ? nested : requested;
                if (changed is null)
                    continue;
                replacements[original] = changed;
                statements.Add(changed);
            }
            var result = statements.ToImmutable();
            return new(rewriteBlock is null ? result : rewriteBlock(result));
        }

        T Replace<T>(T statement) where T : SlangStatement =>
            replacements.TryGetValue(statement, out var found) ? (T)found : statement;
        SlangAssign? ReplaceOptional(SlangAssign? statement) =>
            statement is null ? null : Replace(statement);
        var body = Visit(source.Body);
        var origins = source.Origins with
        {
            Parameters = [.. source.Origins.Parameters.Select(origin => origin with
            {
                Definition = Replace(origin.Definition),
                Capture = ReplaceOptional(origin.Capture)
            })],
            Definitions = [.. source.Origins.Definitions.Select(origin => origin with
            {
                Definition = Replace(origin.Definition),
                Capture = ReplaceOptional(origin.Capture)
            })],
            Transfers = [.. source.Origins.Transfers.Select(origin => origin with
            {
                Arguments = [.. origin.Arguments.Select(argument => argument with
                {
                    Definition = Replace(argument.Definition),
                    Assignment = Replace(argument.Assignment)
                })],
                TokenAssignment = Replace(origin.TokenAssignment),
                Break = Replace(origin.Break)
            })],
            Conditionals = [.. source.Origins.Conditionals.Select(origin => origin with
            {
                Conditional = Replace(origin.Conditional)
            })],
            Gates = [.. source.Origins.Gates.Select(origin => origin with
            {
                Comparison = Replace(origin.Comparison),
                Conditional = Replace(origin.Conditional)
            })]
        };
        return new(source.Declaration, body, origins);
    }

    private static ImmutableArray<SlangStatement> MoveAfter(
        ImmutableArray<SlangStatement> statements,
        SlangStatement moved,
        SlangStatement after)
    {
        var from = ReferenceIndex(statements, moved);
        var destination = ReferenceIndex(statements, after);
        if (from < 0 || destination < 0)
            return statements;
        var builder = statements.ToBuilder();
        builder.RemoveAt(from);
        destination = ReferenceIndex(builder, after);
        builder.Insert(destination + 1, moved);
        return builder.ToImmutable();
    }

    private static ImmutableArray<SlangStatement> WrapTransferInDoOnce(
        ImmutableArray<SlangStatement> statements,
        SlangTransferOrigin transfer)
    {
        var first = transfer.Arguments.IsEmpty
            ? (SlangStatement)transfer.TokenAssignment
            : transfer.Arguments[0].Definition;
        var start = ReferenceIndex(statements, first);
        var end = ReferenceIndex(statements, transfer.Break);
        if (start < 0 || end < start)
            return statements;
        var nested = statements[start..(end + 1)];
        return [.. statements[..start], new SlangDoOnce(new SlangBlock(nested)), .. statements[(end + 1)..]];
    }

    private static int ReferenceIndex(IEnumerable<SlangStatement> statements, SlangStatement expected)
    {
        var index = 0;
        foreach (var statement in statements)
        {
            if (ReferenceEquals(statement, expected))
                return index;
            index++;
        }
        return -1;
    }

    private sealed record PreparedTarget(
        ShaderModuleDeclaration<RegionFunctionBody> Pointer,
        ShaderModuleDeclaration<SlangFunctionBody> Target,
        CLSLCooperationFacts Facts,
        FunctionDeclaration Function);

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

    private static ShaderModuleDeclaration<RegionFunctionBody> PhiConditionalModule(
        bool varyingIncoming = false,
        bool sameTarget = false,
        bool parallel = false,
        string prefix = "",
        bool reverseBindings = false)
    {
        var varyingParameter = new ParameterDeclaration("varying", ShaderType.Bool, []);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [varyingParameter],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create(prefix + "entry");
        var left = Label.Create(prefix + "left");
        var right = Label.Create(prefix + "right");
        var join = Label.Create(prefix + "join");
        var derivative = Label.Create(prefix + "derivative");
        var returned = Label.Create(prefix + "returned");
        var outerCondition = ShaderValue.Intermediate(ShaderType.Bool);
        var varying = ShaderValue.Intermediate(ShaderType.Bool);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var carried = ShaderValue.Intermediate(ShaderType.F32);
        var derivativeValue = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryInstructions = ImmutableArray.CreateBuilder<Instruction<IShaderValue, IShaderValue>>();
        entryInstructions.Add(Instruction<IShaderValue, IShaderValue>.Create(
            new LiteralOperation(),
            outerCondition,
            [ShaderValue.Literal(new BoolLiteral(true))]));
        if (varyingIncoming)
            entryInstructions.Add(Instruction.Factory.Load(
                default,
                new LoadOperation(),
                varying,
                varyingParameter.Value));
        RegionJump<IShaderValue> JumpToJoin(IShaderValue value, float carriedValue) =>
            new(
                join,
                parallel
                    ? [value, ShaderValue.Literal(new F32Literal(carriedValue))]
                    : [value]);
        var entryTerminator = sameTarget
            ? Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                outerCondition,
                JumpToJoin(ShaderValue.Literal(new BoolLiteral(true)), 1.0f),
                JumpToJoin(
                    varyingIncoming ? varying : ShaderValue.Literal(new BoolLiteral(false)),
                    2.0f))
            : Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                outerCondition,
                new(left, []),
                new(right, []));
        var entryBody = RegionFixture.Body(entry, [], entryInstructions, entryTerminator);
        var leftBody = RegionFixture.Body(
            left,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                JumpToJoin(ShaderValue.Literal(new BoolLiteral(true)), 1.0f)));
        var rightBody = RegionFixture.Body(
            right,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                JumpToJoin(
                    varyingIncoming ? varying : ShaderValue.Literal(new BoolLiteral(false)),
                    2.0f)));
        var joinBody = RegionFixture.Body(
            join,
            parallel ? [condition, carried] : [condition],
            [],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition,
                new(derivative, []),
                new(returned, [])));
        var derivativeOperand = parallel ? carried : ShaderValue.Literal(new F32Literal(1.0f));
        var derivativeBody = RegionFixture.Body(
            derivative,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivativeValue,
                    [dpdx, derivativeOperand],
                    "phi-derivative")
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivativeValue));
        var returnedBody = RegionFixture.Body(
            returned,
            [],
            [],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(
                ShaderValue.Literal(new F32Literal(0.0f))));
        var returnedRegion = RegionTree.Block(returned, [], returnedBody, null);
        var derivativeRegion = RegionTree.Block(derivative, [], derivativeBody, null);
        var joinRegion = RegionTree.Block(join, [], joinBody, null);
        var leftRegion = RegionTree.Block(left, [], leftBody, null);
        var rightRegion = RegionTree.Block(right, [], rightBody, null);
        var bindings = reverseBindings
            ? [derivativeRegion, returnedRegion, joinRegion, rightRegion, leftRegion]
            : new[] { returnedRegion, derivativeRegion, joinRegion, leftRegion, rightRegion };
        var selectedBindings = sameTarget
            ? bindings.Where(region => !ReferenceEquals(region.Label, left) &&
                                       !ReferenceEquals(region.Label, right)).ToArray()
            : bindings;
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(entry, [.. selectedBindings], entryBody, join));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> ProviderConditionalModule()
    {
        var declaration = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var derivative = Label.Create("derivative");
        var returned = Label.Create("returned");
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var derivativeValue = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new ProviderOperation(OperationRequirement.None),
                    condition,
                    [])
            ],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition,
                new(derivative, []),
                new(returned, [])));
        var derivativeBody = RegionFixture.Body(
            derivative,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivativeValue,
                    [dpdx, ShaderValue.Literal(new F32Literal(1.0f))])
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivativeValue));
        var returnedBody = RegionFixture.Body(
            returned,
            [],
            [],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(
                ShaderValue.Literal(new F32Literal(0.0f))));
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                entry,
                [
                    RegionTree.Block(returned, [], returnedBody, null),
                    RegionTree.Block(derivative, [], derivativeBody, null)
                ],
                entryBody,
                derivative));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));
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

    private sealed class UniformConditionalDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (UniformChoice())
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        public static bool UniformChoice() => true;
    }

    private sealed class UniformIgnoringArgumentShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Always(varying))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        public static bool Always(float _) => true;
    }

    private sealed class IdentityLiteralConditionalShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Identity(true))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static bool Identity(bool value) => value;
    }

    private sealed class DerivativeConditionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (DMath.dpdx(varying) > 0.0f)
                return varying;
            return DMath.dpdx(varying);
        }
    }

    private sealed class UniformBuiltinCollisionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (DMath.sin(0.0f) >= 0.0f)
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static float sin(float value) => value;
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

    private sealed class UniformSwitchDerivativeShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.dpdx(UniformSelector() switch
            {
                0 => varying,
                1 => varying + 1.0f,
                2 => varying + 2.0f,
                _ => varying + 3.0f
            });

        [ShaderMethod]
        private static int UniformSelector() => 1;
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
