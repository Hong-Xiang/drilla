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
using DualDrill.CLSL.Reflection;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using DualDrill.Graphics;
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
    public void PortableWgslKeepsLoopAdmissionFailClosedBeforeTargetLowering()
    {
        var supported = Emit(new DirectDerivativeShader(), CLSLCompileTarget.WGSL);
        var error = Assert.Throws<NotSupportedException>(() =>
            Emit(new LoopContinueDerivativeShader(), CLSLCompileTarget.WGSL));

        Assert.Contains("dpdx(", supported);
        Assert.Contains("PortableWgsl", error.Message);
        Assert.Contains("RegionKind.Loop", error.Message);
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
            $"helper return dependencies={Format(helperFacts.AggregateReturnDependencies)}");
        output.WriteLine("=== C3 verified target AST ===");
        output.WriteLine(target.GetBody(fragmentDeclaration).PrettyPrint());
        output.WriteLine("=== C3 uniform conditional Slang ===");
        output.WriteLine(slang);
        output.WriteLine("=== C3 uniform conditional WGSL ===");
        output.WriteLine(wgsl);

        Assert.Single(fragmentFacts.UniformConditionals);
        AssertKnown(helperFacts.AggregateReturnDependencies);
        Assert.Contains(nameof(UniformConditionalDerivativeShader.UniformChoice), slang);
        Assert.Contains("if", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void PortableDerivativeCanReadExistingUniformStructStorage()
    {
        var shader = new UniformStorageDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var fragment = Assert.Single(
            raw.FunctionDefinitions,
            item => item.Key.Name == nameof(UniformStorageDerivativeShader.Fragment));
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine("=== C3 uniform storage CIL ===");
        output.WriteLine(fragment.Value.PrettyPrint());
        output.WriteLine("=== C3 uniform storage Slang ===");
        output.WriteLine(slang);
        output.WriteLine("=== C3 uniform storage WGSL ===");
        output.WriteLine(wgsl);

        Assert.Contains("ddx(", slang);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void PortableDerivativeCanReadGuaranteedReadonlyBufferElement()
    {
        var shader = new ReadonlyBufferLoadDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(ReadonlyBufferLoadDerivativeShader.Fragment));
        var load = Assert.Single(
            Instructions(source.GetBody(function)),
            instruction => instruction.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var summary = FunctionEffectAnalysis.Analyze(source)[function];
        var participation = Assert.Single(CLSLCooperationAnalysis.Analyze(source).EntryUniformQuadParticipations);
        var storage = Assert.Single(new ShaderModuleReflection().GetStorageBufferBindings(raw));
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine("host obligation: the reflected four-byte minimum binding contains element zero");
        output.WriteLine(slang);
        output.WriteLine(wgsl);

        var site = Assert.Single(summary.RequirementSites, candidate =>
            candidate.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Equal(OperationRequirement.MemoryRead, site.Requirements);
        Assert.Same(load.Payload, site.Payload);
        Assert.Contains(participation.OriginalRelevantInstructions, fact =>
            ReferenceEquals(fact.Function, function) &&
            fact.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>> &&
            ReferenceEquals(fact.Result, load.Result) &&
            ReferenceEquals(fact.Payload, load.Payload));
        Assert.Equal(4ul, storage.MinimumBindingSize);
        Assert.Matches(@"v_\d+_Input\[v_\d+\]", slang);
        Assert.Contains("ddx(", slang);
        Assert.Matches(@"Input_0\[u32\(0\)\]", wgsl);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void PortableDerivativeCanUseReadonlyBufferLengthAsData()
    {
        var shader = new ReadonlyBufferLengthDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(ReadonlyBufferLengthDerivativeShader.Fragment));
        var length = Assert.Single(
            Instructions(source.GetBody(function)),
            instruction => instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var summary = FunctionEffectAnalysis.Analyze(source)[function];
        var participation = Assert.Single(CLSLCooperationAnalysis.Analyze(source).EntryUniformQuadParticipations);
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine(slang);
        output.WriteLine(wgsl);

        Assert.DoesNotContain(summary.RequirementSites, site =>
            site.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>);
        Assert.Contains(participation.OriginalRelevantInstructions, fact =>
            ReferenceEquals(fact.Function, function) &&
            fact.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>> &&
            ReferenceEquals(fact.Result, length.Result) &&
            ReferenceEquals(fact.Payload, length.Payload) &&
            fact.Operands.AsEnumerable().SequenceEqual(
                length.Operands,
                ReferenceEqualityComparer.Instance));
        Assert.Contains(".GetDimensions(", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("arrayLength(", wgsl);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void IntegerLoadAndLengthRetainExactCooperationOrigins()
    {
        var prepared = PrepareTarget(new IntegerResourceDerivativeShader());
        CooperationAdmission.CheckTargetCorrespondence(
            prepared.Pointer, prepared.Target, prepared.Facts);
        var body = prepared.Target.GetBody(prepared.Function);
        var load = Assert.Single(body.Origins.Instructions, origin =>
            origin.Source.Operation is StructuredBufferLoadOperation<IntType<N32>>);
        var dimensions = Assert.Single(body.Origins.Dimensions);
        Assert.IsType<StructuredBufferLengthOperation<IntType<N32>>>(dimensions.Source.Operation);
        var effect = FunctionEffectAnalysis.Analyze(prepared.Pointer)[prepared.Function];
        Assert.True(effect.IsComplete);
        Assert.DoesNotContain(effect.RequirementSites, site =>
            site.Operation is StructuredBufferLengthOperation<IntType<N32>>);
        Assert.Equal(OperationRequirement.MemoryRead, Assert.Single(
            effect.RequirementSites, site =>
                site.Operation is StructuredBufferLoadOperation<IntType<N32>>).Requirements);

        var changedLoad = Rewrite(body, statement =>
            ReferenceEquals(statement, load.Target) && statement is SlangBind bind
                ? new SlangBind(bind.Instruction with { Payload = new object() })
                : statement);
        var changedCount = Rewrite(body, statement =>
            ReferenceEquals(statement, dimensions.Dimensions)
                ? dimensions.Dimensions with { Count = ShaderValue.Intermediate(ShaderType.U32) }
                : statement);
        foreach (var changed in new[] { changedLoad, changedCount })
        {
            var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
                prepared.Target.Declarations,
                prepared.Target.FunctionDefinitions.SetItem(prepared.Function, changed));
            Assert.Throws<NotSupportedException>(() =>
                CooperationAdmission.CheckTargetCorrespondence(
                    prepared.Pointer, corrupted, prepared.Facts));
        }
    }

    [Fact]
    public void PortableUniformHelperCarriesReadonlyLengthAcrossBlocks()
    {
        var shader = new ReadonlyBufferLengthAcrossBlocksShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var fragment = Assert.Single(
            raw.FunctionDefinitions,
            item => item.Key.Name == nameof(ReadonlyBufferLengthAcrossBlocksShader.Fragment));
        Assert.Contains(
            fragment.Value.Code.Instructions,
            item => item.Instruction.OpCode.Name?.StartsWith("brtrue", StringComparison.Ordinal) is true ||
                    item.Instruction.OpCode.Name?.StartsWith("brfalse", StringComparison.Ordinal) is true);
        Assert.Contains(
            fragment.Value.Code.Instructions,
            item => item.Instruction.Operand is System.Reflection.MethodInfo
            {
                Name: nameof(ReadonlyBufferLengthAcrossBlocksShader.UniformChoice)
            });

        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        CooperationAdmission.CheckTargetCorrespondence(pointer, target, facts);
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(ReadonlyBufferLengthAcrossBlocksShader.Fragment));
        var targetBody = target.GetBody(function);
        var dimensions = Assert.Single(
            targetBody.Origins.Dimensions,
            origin => ReferenceEquals(origin.Label, source.GetBody(function).Entry));
        var length = Assert.Single(
            Instructions(source.GetBody(function)),
            instruction => ReferenceEquals(instruction.Result, dimensions.Dimensions.Count));
        var countCarrier = Assert.Single(targetBody.Origins.Definitions, definition =>
            definition.Source.Operands.Any(operand =>
                ReferenceEquals(operand, dimensions.Dimensions.Count)) &&
            targetBody.Origins.Transfers.SelectMany(transfer => transfer.Arguments)
                .Any(argument => ReferenceEquals(argument.Argument, definition.Source.Result)));
        var transferArguments = targetBody.Origins.Transfers
            .SelectMany(transfer => transfer.Arguments)
            .Where(argument => ReferenceEquals(argument.Argument, countCarrier.Source.Result))
            .ToArray();
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine(fragment.Value.PrettyPrint());
        output.WriteLine(targetBody.PrettyPrint());
        output.WriteLine(slang);
        output.WriteLine(wgsl);

        Assert.Same(length.Result, dimensions.Dimensions.Count);
        Assert.NotEmpty(transferArguments);
        Assert.All(transferArguments, transferArgument =>
        {
            Assert.Same(countCarrier.Source.Result, transferArgument.Argument);
            Assert.Same(
                countCarrier.Source.Result,
                Assert.IsType<SlangValueOperand>(
                    transferArgument.Definition.Instruction.Operands.Single()).Value);
            Assert.Same(
                targetBody.Origins.ParameterSlots[transferArgument.Parameter],
                transferArgument.Slot);
            Assert.True(AreInOrder(
                targetBody.Body,
                transferArgument.Definition,
                transferArgument.Assignment));
        });
        Assert.Contains(".GetDimensions(", slang);
        Assert.Contains("if", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("arrayLength(", wgsl);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void ComputeTextureSampleWritesOneChannelToGuardedWritableOutput()
    {
        var shader = new TextureToWritableComputeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var method = Assert.Single(
            raw.FunctionDefinitions,
            item => item.Key.Name == nameof(TextureToWritableComputeShader.Run));
        Assert.Contains(
            method.Value.Code.Instructions,
            item => item.Instruction.OpCode.FlowControl is System.Reflection.Emit.FlowControl.Cond_Branch);
        var compiled = new CLSLCompiler(new(CLSLCompileTarget.IR)).Compile(raw);
        var source = compiled.RunPass(new FunctionToOperationPass());
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(TextureToWritableComputeShader.Run));
        var operations = Instructions(source.GetBody(function)).ToArray();
        var length = Assert.Single(operations, instruction =>
            instruction.Operation is ReadWriteStructuredBufferLengthOperation);
        var sample = Assert.Single(operations, instruction =>
            instruction.Operation is TextureSampleLevelOperation);
        var store = Assert.Single(operations, instruction =>
            instruction.Operation is ReadWriteStructuredBufferStoreOperation);
        var effects = FunctionEffectAnalysis.Analyze(source)[function];
        var reflection = new ShaderModuleReflection();
        var storage = Assert.Single(reflection.GetStorageBufferBindings(raw));
        var texture = Assert.Single(reflection.GetTextureBindings(raw));
        var sampler = Assert.Single(reflection.GetSamplerBindings(raw));
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);

        output.WriteLine(method.Value.PrettyPrint());
        output.WriteLine(slang);
        output.WriteLine(wgsl);
        output.WriteLine("source oracle: one sampled RGBA value contributes only its x channel to Output[0]");

        Assert.DoesNotContain(effects.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferLengthOperation);
        var read = Assert.Single(effects.RequirementSites, site =>
            site.Operation is TextureSampleLevelOperation);
        var write = Assert.Single(effects.RequirementSites, site =>
            site.Operation is ReadWriteStructuredBufferStoreOperation);
        Assert.Equal(OperationRequirement.MemoryRead, read.Requirements);
        Assert.Equal(OperationRequirement.MemoryWrite, write.Requirements);
        Assert.Same(sample.Payload, read.Payload);
        Assert.Same(store.Payload, write.Payload);
        Assert.NotNull(length.Payload);
        Assert.Equal(GPUBufferBindingType.Storage, storage.Kind);
        Assert.Equal(4ul, storage.MinimumBindingSize);
        Assert.Equal(2, texture.Binding);
        Assert.Equal(3, sampler.Binding);
        Assert.Contains(".GetDimensions(", slang);
        Assert.Contains(".SampleLevel(", slang);
        Assert.Matches(@"v_\d+_Output\[v_\d+\] = ", slang);
        Assert.Contains("arrayLength(", wgsl);
        Assert.Contains("textureSampleLevel(", wgsl);
        Assert.Contains("var<storage, read_write>", wgsl);
    }

    [Fact]
    public void PortableDerivativeConsumesExplicitLodSampleAsData()
    {
        var shader = new TextureSampleDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(TextureSampleDerivativeShader.Fragment));
        var sample = Assert.Single(
            Instructions(source.GetBody(function)),
            instruction => instruction.Operation is TextureSampleLevelOperation);
        var effects = FunctionEffectAnalysis.Analyze(source)[function];
        var participation = Assert.Single(CLSLCooperationAnalysis.Analyze(source)
            .EntryUniformQuadParticipations);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        CooperationAdmission.CheckTargetCorrespondence(pointer, target, new([participation]));
        var sampleOrigin = Assert.Single(target.GetBody(function).Origins.Definitions, origin =>
            origin.Source.Operation is TextureSampleLevelOperation);
        var reflection = new ShaderModuleReflection();
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine(slang);
        output.WriteLine(wgsl);

        var read = Assert.Single(effects.RequirementSites, site =>
            site.Operation is TextureSampleLevelOperation);
        Assert.Equal(OperationRequirement.MemoryRead, read.Requirements);
        Assert.Equal(OperationRequirement.None, read.Requirements & OperationRequirement.DerivativeQuad);
        Assert.Same(sample.Payload, read.Payload);
        Assert.Contains(participation.OriginalRelevantInstructions, fact =>
            fact.Operation is TextureSampleLevelOperation &&
            ReferenceEquals(fact.Result, sample.Result) &&
            ReferenceEquals(fact.Payload, sample.Payload));
        Assert.Same(sample.Result, sampleOrigin.Definition.Instruction.Result);
        Assert.Single(reflection.GetTextureBindings(raw));
        Assert.Single(reflection.GetSamplerBindings(raw));
        Assert.Empty(reflection.GetStorageBufferBindings(raw));
        Assert.Contains(".SampleLevel(", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("textureSampleLevel(", wgsl);
        Assert.Contains("dpdx(", wgsl);
        Assert.DoesNotContain("RWStructuredBuffer", slang);
        Assert.DoesNotContain("read_write", wgsl);
    }

    [Fact]
    public void PortableTextureSampleDataCrossesSourceLabelsBeforeDerivative()
    {
        var shader = new CrossLabelTextureSampleDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var rawFunction = Assert.Single(
            raw.FunctionDefinitions,
            item => item.Key.Name == nameof(CrossLabelTextureSampleDerivativeShader.Fragment));
        Assert.Contains(
            rawFunction.Value.Code.Instructions,
            item => item.Instruction.OpCode.FlowControl is System.Reflection.Emit.FlowControl.Cond_Branch);
        Assert.Contains(
            rawFunction.Value.Code.Instructions,
            item => item.Instruction.Operand is System.Reflection.MethodInfo
            {
                Name: nameof(CrossLabelTextureSampleDerivativeShader.UniformChoice)
            });

        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        CooperationAdmission.CheckTargetCorrespondence(pointer, target, facts);
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(CrossLabelTextureSampleDerivativeShader.Fragment));
        var sourceBody = source.GetBody(function);
        var pointerBody = pointer.GetBody(function);
        var sites = pointerBody.Labels
            .SelectMany(label => pointerBody[label].Body.Elements.Select(
                (instruction, ordinal) => (Label: label, Ordinal: ordinal, Instruction: instruction)))
            .ToArray();
        var sample = Assert.Single(sites, site =>
            site.Instruction.Operation is TextureSampleLevelOperation);
        var definitions = sites
            .Where(site => site.Instruction.Result is not null)
            .ToDictionary(
                site => site.Instruction.Result!,
                site => site,
                ReferenceEqualityComparer.Instance);
        bool IsSampledData(IShaderValue value)
        {
            var visited = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
            return Trace(value);

            bool Trace(IShaderValue candidate)
            {
                if (!visited.Add(candidate))
                    return false;
                if (ReferenceEquals(candidate, sample.Instruction.Result))
                    return true;
                if (definitions.TryGetValue(candidate, out var definition) &&
                    definition.Instruction.Operands.Any(Trace))
                    return true;
                return sites.Any(site =>
                    site.Instruction.Operation is StoreOperation &&
                    ReferenceEquals(site.Instruction.Operand0, candidate) &&
                    Trace(site.Instruction.Operand1!));
            }
        }

        var targetBody = target.GetBody(function);
        output.WriteLine(rawFunction.Value.PrettyPrint());
        output.WriteLine(sourceBody.Dump());
        output.WriteLine(pointerBody.Dump());
        output.WriteLine(targetBody.PrettyPrint());
        var carried = Assert.Single(targetBody.Origins.Captures.Keys, IsSampledData);
        var definitionSite = definitions[carried];
        var useSite = Assert.Single(sites, site =>
            !ReferenceEquals(site.Label, definitionSite.Label) &&
            site.Instruction.Operands.Any(operand => ReferenceEquals(operand, carried)));
        var definitionOrigin = Assert.Single(targetBody.Origins.Definitions, origin =>
            ReferenceEquals(origin.Source.Result, carried));
        var capture = Assert.IsType<SlangAssign>(definitionOrigin.Capture);
        var carrier = Assert.IsType<SlangVariablePlace>(capture.Target).Variable;
        var consumer = Assert.Single(targetBody.Origins.Instructions, origin =>
            ReferenceEquals(origin.Label, useSite.Label) &&
            origin.InstructionOrdinal == useSite.Ordinal);
        var consumerInstruction = Assert.IsType<SlangBind>(consumer.Target).Instruction;
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        output.WriteLine(slang);
        output.WriteLine(wgsl);

        Assert.NotSame(definitionSite.Label, useSite.Label);
        Assert.Same(carried, Assert.IsType<SlangValueOperand>(capture.Value).Value);
        Assert.Same(targetBody.Origins.Captures[carried], carrier);
        Assert.True(AreAdjacent(targetBody.Body, definitionOrigin.Definition, capture));
        Assert.Contains(consumerInstruction.Operands, operand =>
            operand is SlangPlaceOperand
            {
                Place: SlangVariablePlace { Variable: var variable }
            } &&
            ReferenceEquals(variable, carrier));
        Assert.Contains(TextureSampleLevelOperation.Instance.Name, new CLSLCompiler(new(
            CLSLCompileTarget.IR,
            CLSLCooperationProfile.PortableWgsl)).Emit(shader));
        Assert.Contains(".SampleLevel(", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("textureSampleLevel(", wgsl);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void PortableTextureSampleRetainsSensitiveArgumentRequirements()
    {
        var shader = new TextureSampleSensitiveArgumentShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var function = source.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(TextureSampleSensitiveArgumentShader.Fragment));
        var summary = FunctionEffectAnalysis.Analyze(source)[function];
        var participation = Assert.Single(CLSLCooperationAnalysis.Analyze(source)
            .EntryUniformQuadParticipations);
        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);

        Assert.Contains(summary.RequirementSites, site =>
            site.Operation is TextureSampleLevelOperation &&
            site.Requirements == OperationRequirement.MemoryRead);
        Assert.Contains(summary.RequirementSites, site =>
            (site.Requirements & OperationRequirement.DerivativeQuad) != 0);
        Assert.Contains(participation.OriginalRelevantInstructions, fact =>
            fact.Operation is TextureSampleLevelOperation);
        Assert.Contains(".SampleLevel(", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("textureSampleLevel(", wgsl);
        Assert.Contains("dpdx(", wgsl);
    }

    [Fact]
    public void UnreferencedNonlocalStorageIsNotInitiallyDefined()
    {
        var shader = new UniformStorageDerivativeShader();
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var original = Assert.Single(source.Declarations.OfType<VariableDeclaration>());
        var unreferenced = new VariableDeclaration(
            original.AddressSpace,
            "unreferenced",
            original.Type,
            [
                .. original.Attributes.Where(static attribute => attribute is not BindingAttribute),
                new BindingAttribute(1)
            ]);
        source = new(
            [.. source.Declarations, unreferenced],
            source.FunctionDefinitions);
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        var entry = target.FunctionDefinitions.Keys.Single(function =>
            function.Name == nameof(UniformStorageDerivativeShader.Fragment));
        var body = target.GetBody(entry);
        var load = body.Origins.Definitions.Single(origin =>
            origin.Source.Operation is LoadOperation &&
            origin.Definition.Instruction.Operands.Single() is SlangPlaceOperand
            {
                Place: SlangMemberPlace
            });
        var member = Assert.IsType<SlangMemberPlace>(
            Assert.IsType<SlangPlaceOperand>(load.Definition.Instruction.Operands.Single()).Place);
        var corruptedLoad = new SlangBind(
            load.Definition.Instruction.Select(
                operand => operand is SlangPlaceOperand { Place: SlangMemberPlace }
                    ? new SlangPlaceOperand(new SlangMemberPlace(
                        new SlangVariablePlace(unreferenced),
                        member.Member))
                    : operand,
                static result => result));
        var corruptedBody = Rewrite(
            body,
            statement => ReferenceEquals(statement, load.Definition)
                ? corruptedLoad
                : statement);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            target.Declarations,
            target.FunctionDefinitions.SetItem(entry, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(pointer, corrupted, facts));

        Assert.Contains("without a reaching definition", error.Message);
    }

    [Fact]
    public void ErasedUnusedGlobalPointerDoesNotRemainAnActiveStorageRoot()
    {
        var fixture = CreateUnusedGlobalPointerFixture();
        var prepared = PrepareTarget(fixture.Module);
        var sourceBody = fixture.Module.GetBody(fixture.Function);
        var pointerBody = prepared.Pointer.GetBody(fixture.Function);
        var sourceJump = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            sourceBody[sourceBody.Entry].Body.Last).Target;
        var pointerJump = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            pointerBody[pointerBody.Entry].Body.Last).Target;
        var uniformity = Assert.Single(
            Assert.Single(prepared.Facts.EntryUniformQuadParticipations).Uniformity.Values);

        Assert.Single(sourceBody[fixture.Body].Parameters);
        Assert.Single(sourceJump.Arguments);
        Assert.Same(fixture.Storage.Value, sourceJump.Arguments[0]);
        Assert.Single(uniformity.OriginalNonFunctionStorage);
        Assert.Same(fixture.Storage, uniformity.OriginalNonFunctionStorage[0]);
        Assert.Empty(pointerBody[fixture.Body].Parameters);
        Assert.Empty(pointerJump.Arguments);
    }

    [Fact]
    public void ContextIndependentLiteralHelperIgnoresVaryingArgument()
    {
        var facts = Analyze(new UniformIgnoringArgumentShader());
        var helper = Assert.Single(
            facts.EntryUniformQuadParticipations[0].Uniformity.Values,
            item => item.Function.Name == nameof(UniformIgnoringArgumentShader.Always));

        AssertKnown(helper.AggregateReturnDependencies);
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
        Assert.Equal(2, function.DependencyBindings.Count(binding => binding.ParameterPosition == 0));
        Assert.Contains(
            function.DependencyValues,
            fact => fact.Kind is CooperationDependencyValueKind.BlockParameter);
    }

    [Fact]
    public void SameTargetConditionalArmsRemainDistinctAndParallel()
    {
        var module = PhiConditionalModule(sameTarget: true, parallel: true);
        var facts = CLSLCooperationAnalysis.Analyze(module);
        var participation = Assert.Single(facts.EntryUniformQuadParticipations);
        var function = Assert.Single(participation.Uniformity.Values);
        var entry = function.OriginalBlocks[0];
        var bindings = function.DependencyBindings.Where(binding =>
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
    public void DependencyFactsIgnoreLabelNamesAndRegionStorageOrder()
    {
        var left = Assert.Single(
            CLSLCooperationAnalysis.Analyze(PhiConditionalModule(prefix: "left-", reverseBindings: false))
                .EntryUniformQuadParticipations).Uniformity.Values.Single();
        var right = Assert.Single(
            CLSLCooperationAnalysis.Analyze(PhiConditionalModule(prefix: "right-", reverseBindings: true))
                .EntryUniformQuadParticipations).Uniformity.Values.Single();

        Assert.Equal(left.OriginalBlocks.Length, right.OriginalBlocks.Length);
        Assert.Equal(left.DependencyValues.Length, right.DependencyValues.Length);
        Assert.Equal(left.DependencyBindings.Length, right.DependencyBindings.Length);
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
            AssertVerifierRejection(error, item.Expected);
        }
    }

    [Fact]
    public void TargetVerifierRejectsCorruptedDimensionsOriginsAndCarriers()
    {
        var prepared = PrepareTarget(DimensionsCaptureModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var origin = Assert.Single(body.Origins.Dimensions);
        var dimensions = origin.Dimensions;
        var capture = Assert.IsType<SlangAssign>(origin.Capture);
        var wrongBuffer = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "wrong_buffer",
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(7)]);
        var cases = new (string Name, Func<SlangFunctionBody> Mutate, string Expected)[]
        {
            (
                "changed-buffer-root-with-updated-origin",
                () =>
                {
                    var changed = dimensions with
                    {
                        Buffer = new SlangPlaceOperand(new SlangVariablePlace(wrongBuffer))
                    };
                    return Rewrite(
                        body,
                        statement => ReferenceEquals(statement, dimensions) ? changed : statement);
                },
                "dimensions origin changed its source operation/result/operand lineage"),
            (
                "changed-count-with-updated-origin",
                () =>
                {
                    var changed = dimensions with
                    {
                        Count = ShaderValue.Intermediate(ShaderType.U32)
                    };
                    return Rewrite(
                        body,
                        statement => ReferenceEquals(statement, dimensions) ? changed : statement);
                },
                "dimensions origin changed its source operation/result/operand lineage"),
            (
                "aliased-stride-with-updated-origin",
                () =>
                {
                    var changed = dimensions with { Stride = dimensions.Count };
                    return Rewrite(
                        body,
                        statement => ReferenceEquals(statement, dimensions) ? changed : statement);
                },
                "dimensions stride is not a fresh writable u32 intermediate"),
            (
                "wrong-stride-type-with-updated-origin",
                () =>
                {
                    var changed = dimensions with
                    {
                        Stride = ShaderValue.Intermediate(ShaderType.F32)
                    };
                    return Rewrite(
                        body,
                        statement => ReferenceEquals(statement, dimensions) ? changed : statement);
                },
                "dimensions stride is not a fresh writable u32 intermediate"),
            (
                "duplicate-dimensions-with-updated-origins",
                () =>
                {
                    var duplicate = dimensions with
                    {
                        Stride = ShaderValue.Intermediate(ShaderType.U32)
                    };
                    var changed = Rewrite(
                        body,
                        static statement => statement,
                        statements => InsertAfter(statements, dimensions, duplicate));
                    return new SlangFunctionBody(
                        changed.Declaration,
                        changed.Body,
                        changed.Origins with
                        {
                            Dimensions =
                            [
                                .. changed.Origins.Dimensions,
                                origin with { Dimensions = duplicate }
                            ]
                        });
                },
                "activation template has duplicate source instruction origins"),
            (
                "dimensions-moved-outside-source-scope",
                () =>
                {
                    var changed = Rewrite(
                        body,
                        statement => ReferenceEquals(statement, dimensions) ? null : statement);
                    return new SlangFunctionBody(
                        changed.Declaration,
                        new SlangBlock([dimensions, .. changed.Body.Statements]),
                        changed.Origins);
                },
                "activation template contains a misplaced declaration"),
            (
                "cross-block-count-capture-reads-stride-with-updated-origin",
                () =>
                {
                    var changedCapture = new SlangAssign(
                        capture.Target,
                        new SlangValueOperand(dimensions.Stride));
                    return Rewrite(
                        body,
                        statement => ReferenceEquals(statement, capture) ? changedCapture : statement);
                },
                "captured dependency definition writes the wrong value or carrier")
        };

        foreach (var item in cases)
        {
            var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
                prepared.Target.Declarations,
                prepared.Target.FunctionDefinitions.SetItem(
                    prepared.Function,
                    item.Mutate()));
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
    public void DirectCrossBlockLengthUsesImmediateCountCapture()
    {
        var prepared = PrepareTarget(DimensionsCaptureModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var origin = Assert.Single(body.Origins.Dimensions);
        var capture = Assert.IsType<SlangAssign>(origin.Capture);
        var target = Assert.IsType<SlangVariablePlace>(capture.Target);
        var value = Assert.IsType<SlangValueOperand>(capture.Value);

        Assert.Same(origin.Source.Result, origin.Dimensions.Count);
        Assert.Same(origin.Dimensions.Count, value.Value);
        Assert.Same(body.Origins.Captures[origin.Dimensions.Count], target.Variable);
        Assert.True(AreAdjacent(body.Body, origin.Dimensions, capture));
    }

    [Fact]
    public void TargetVerifierRejectsTextureSampleLineageMutationsAndMissingOrigins()
    {
        var prepared = PrepareTarget(new TextureSampleDerivativeShader());
        var body = prepared.Target.GetBody(prepared.Function);
        var sample = Assert.Single(body.Origins.Definitions, origin =>
            origin.Source.Operation is TextureSampleLevelOperation);
        var instruction = sample.Definition.Instruction;
        var operands = instruction.Operands.ToImmutableArray();
        Assert.Equal(4, operands.Length);
        var wrongTexture = new VariableDeclaration(
            HandleAddressSpace.Instance,
            "wrong_texture",
            SampledTexture2DF32Type.Instance,
            [new GroupAttribute(0), new BindingAttribute(7)]);
        var wrongSampler = new VariableDeclaration(
            HandleAddressSpace.Instance,
            "wrong_sampler",
            SamplerStateType.Instance,
            [new GroupAttribute(0), new BindingAttribute(8)]);
        var cases = new (string Name, Func<SlangFunctionBody> Mutate, string Expected)[]
        {
            (
                "texture-root-with-updated-origins",
                () => ReplaceSample(instruction with
                {
                    Operand0 = new SlangPlaceOperand(new SlangVariablePlace(wrongTexture))
                }),
                "instruction origin changed its source operation/result/operand lineage"),
            (
                "sampler-root-with-updated-origins",
                () => ReplaceSample(instruction with
                {
                    Operand1 = new SlangPlaceOperand(new SlangVariablePlace(wrongSampler))
                }),
                "instruction origin changed its source operation/result/operand lineage"),
            (
                "uv-with-updated-origins",
                () => ReplaceSample(instruction with
                {
                    RestOperands =
                    [
                        new SlangValueOperand(ShaderValue.Intermediate(ShaderType.Vec2F32)),
                        operands[3]
                    ]
                }),
                "instruction origin changed its source operation/result/operand lineage"),
            (
                "lod-with-updated-origins",
                () => ReplaceSample(instruction with
                {
                    RestOperands =
                    [
                        operands[2],
                        new SlangValueOperand(ShaderValue.Intermediate(ShaderType.F32))
                    ]
                }),
                "instruction origin changed its source operation/result/operand lineage"),
            (
                "result-with-updated-origins",
                () => ReplaceSample(instruction with
                {
                    Result = ShaderValue.Intermediate(ShaderType.Vec4F32)
                }),
                "definition origin changed its source operation/result/operand lineage"),
            (
                "missing-sample-origins",
                () => new SlangFunctionBody(
                    body.Declaration,
                    body.Body,
                    body.Origins with
                    {
                        Definitions = [.. body.Origins.Definitions.Where(origin =>
                            !ReferenceEquals(origin.Definition, sample.Definition))],
                        Instructions = [.. body.Origins.Instructions.Where(origin =>
                            !ReferenceEquals(origin.Target, sample.Definition))]
                    }),
                "activation template is missing a source instruction")
        };

        SlangFunctionBody ReplaceSample(
            Instruction<SlangOperand, IShaderValue> changedInstruction)
        {
            var changed = new SlangBind(changedInstruction);
            return Rewrite(
                body,
                statement => ReferenceEquals(statement, sample.Definition) ? changed : statement);
        }

        foreach (var item in cases)
        {
            var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
                prepared.Target.Declarations,
                prepared.Target.FunctionDefinitions.SetItem(prepared.Function, item.Mutate()));
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
    public void StaleTextureSampleFactCannotCertifyChangedSource()
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create())
            .ParseShaderModule(new TextureSampleDerivativeShader());
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var function = pointer.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(TextureSampleDerivativeShader.Fragment));
        var body = pointer.GetBody(function);
        var site = body.Labels
            .SelectMany(label => body[label].Body.Elements.Select(
                (instruction, ordinal) => (Label: label, Ordinal: ordinal, Instruction: instruction)))
            .Single(item => item.Instruction.Operation is TextureSampleLevelOperation);
        var corruptedBody = body.MapRegionBody(block =>
            !ReferenceEquals(block.Label, site.Label)
                ? block
                : block with
                {
                    Body = Seq.Create(
                        block.Body.Elements.Select((instruction, ordinal) =>
                            ordinal == site.Ordinal
                                ? instruction with { Payload = new object() }
                                : instruction),
                        block.Body.Last)
                });
        var corruptedPointer = new ShaderModuleDeclaration<RegionFunctionBody>(
            pointer.Declarations,
            pointer.FunctionDefinitions.SetItem(function, corruptedBody));
        var target = new SlangTargetLowering().Lower(corruptedPointer);

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                corruptedPointer,
                target,
                facts));

        Assert.Contains("relevant operation fact", error.Message);
        Assert.Contains("does not match the analyzed source", error.Message);
    }

    [Fact]
    public void TargetVerifierRequiresSourceDerivedTextureSampleCapture()
    {
        var prepared = PrepareTarget(TextureCaptureModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var sample = Assert.Single(body.Origins.Definitions, origin =>
            origin.Source.Operation is TextureSampleLevelOperation);
        var capture = Assert.IsType<SlangAssign>(sample.Capture);
        var carrier = Assert.IsType<SlangVariablePlace>(capture.Target).Variable;
        var derivative = Assert.Single(body.Origins.Definitions, origin =>
            origin.Source.Operation is CallOperation &&
            Equals(origin.Source.Payload, "captured-sample-derivative"));
        var changedDerivative = new SlangBind(
            derivative.Definition.Instruction with
            {
                Operand1 = new SlangValueOperand(sample.Source.Result!)
            });
        var changed = Rewrite(body, statement =>
            ReferenceEquals(statement, capture) ||
            statement is SlangDeclare declaration && ReferenceEquals(declaration.Variable, carrier)
                ? null
                : ReferenceEquals(statement, derivative.Definition)
                    ? changedDerivative
                    : statement);
        changed = new SlangFunctionBody(
            changed.Declaration,
            changed.Body,
            changed.Origins with
            {
                Captures = changed.Origins.Captures.Remove(sample.Source.Result!),
                Definitions = [.. changed.Origins.Definitions.Select(origin =>
                    ReferenceEquals(origin.Source.Result, sample.Source.Result)
                        ? origin with { Capture = null }
                        : origin)]
            });
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, changed));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("source value crosses source-label scope without its required capture", error.Message);
    }

    [Fact]
    public void TargetVerifierRequiresSourceDerivedDimensionsCapture()
    {
        var prepared = PrepareTarget(DimensionsCaptureModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var origin = Assert.Single(body.Origins.Dimensions);
        var capture = Assert.IsType<SlangAssign>(origin.Capture);
        var carrier = Assert.IsType<SlangVariablePlace>(capture.Target).Variable;
        var load = Assert.Single(body.Origins.Definitions, candidate =>
            candidate.Source.Operation is StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>);
        var changedLoad = new SlangBind(
            load.Definition.Instruction with
            {
                Operand1 = new SlangValueOperand(origin.Dimensions.Count)
            });
        var changed = Rewrite(body, statement =>
            ReferenceEquals(statement, capture) ||
            statement is SlangDeclare declaration && ReferenceEquals(declaration.Variable, carrier)
                ? null
                : ReferenceEquals(statement, load.Definition)
                    ? changedLoad
                    : statement);
        changed = new SlangFunctionBody(
            changed.Declaration,
            changed.Body,
            changed.Origins with
            {
                Captures = changed.Origins.Captures.Remove(origin.Dimensions.Count),
                Dimensions =
                [
                    origin with
                    {
                        Dimensions = changed.Origins.Dimensions.Single().Dimensions,
                        Capture = null
                    }
                ]
            });
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, changed));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("source value crosses source-label scope without its required capture", error.Message);
    }

    [Fact]
    public void StaleDependencyFactsCannotCertifyChangedPointerProducer()
    {
        var module = PhiConditionalModule();
        var function = Assert.Single(module.FunctionDefinitions.Keys);
        var body = module.GetBody(function);
        var condition = Assert.Single(body[body.Entry].Body.Elements).Result!;
        var varying = ShaderValue.Intermediate(ShaderType.Bool);
        var literalTrue = ShaderValue.Literal(new BoolLiteral(true));
        var load = Instruction<IShaderValue, IShaderValue>.Create(
            new LoadOperation(),
            varying,
            [function.Parameters[0].Value]);
        var uniform = Instruction<IShaderValue, IShaderValue>.Create(
            LogicalBinaryOperation<LogicalAnd>.Instance,
            condition,
            [literalTrue, literalTrue],
            "uniform-and");
        body = body.MapRegionBody(block => ReferenceEquals(block.Label, body.Entry)
            ? block with { Body = Seq.Create(new[] { load, uniform }, block.Body.Last) }
            : block);
        module = new(
            module.Declarations,
            module.FunctionDefinitions.SetItem(function, body));
        var facts = CLSLCooperationAnalysis.Analyze(module);
        var pointer = module.RunPass(new StablePointerRegionParameterPass());
        var corruptedBody = pointer.GetBody(function).MapRegionBody(block =>
            ReferenceEquals(block.Label, body.Entry)
                ? block with
                {
                    Body = Seq.Create(
                        new[]
                        {
                            load,
                            Instruction<IShaderValue, IShaderValue>.Create(
                                uniform.Operation,
                                condition,
                                [varying, literalTrue],
                                uniform.Payload)
                        },
                        block.Body.Last)
                }
                : block);
        var corruptedPointer = new ShaderModuleDeclaration<RegionFunctionBody>(
            pointer.Declarations,
            pointer.FunctionDefinitions.SetItem(function, corruptedBody));

        var fresh = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(corruptedPointer));
        var stale = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                corruptedPointer,
                new SlangTargetLowering().Lower(corruptedPointer),
                facts));

        Assert.Contains("conditional control is varying", fresh.Message);
        Assert.Contains("dependency proof", stale.Message);
    }

    [Fact]
    public void StaleResourceFactsCannotCertifyChangedLengthOrLoad()
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create())
            .ParseShaderModule(new ReadonlyBufferLengthDerivativeShader());
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var function = pointer.FunctionDefinitions.Keys.Single(candidate =>
            candidate.Name == nameof(ReadonlyBufferLengthDerivativeShader.Fragment));
        var body = pointer.GetBody(function);
        var sites = body.Labels
            .SelectMany(label => body[label].Body.Elements.Select(
                (instruction, ordinal) => (Label: label, Ordinal: ordinal, Instruction: instruction)))
            .Where(site =>
                site.Instruction.Operation is StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>> or StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>)
            .ToArray();
        Assert.Equal(2, sites.Length);

        foreach (var site in sites)
        {
            var corruptedBody = body.MapRegionBody(block =>
                !ReferenceEquals(block.Label, site.Label)
                    ? block
                    : block with
                    {
                        Body = Seq.Create(
                            block.Body.Elements.Select((instruction, ordinal) =>
                                ordinal == site.Ordinal
                                    ? instruction with { Payload = new object() }
                                    : instruction),
                            block.Body.Last)
                    });
            var corruptedPointer = new ShaderModuleDeclaration<RegionFunctionBody>(
                pointer.Declarations,
                pointer.FunctionDefinitions.SetItem(function, corruptedBody));
            var target = new SlangTargetLowering().Lower(corruptedPointer);

            var error = Assert.Throws<NotSupportedException>(() =>
                CooperationAdmission.CheckTargetCorrespondence(
                    corruptedPointer,
                    target,
                    facts));

            Assert.Contains("relevant operation fact", error.Message);
            Assert.Contains("does not match the analyzed source", error.Message);
        }
    }

    [Fact]
    public void TargetVerifierRejectsAliasedUniformAndVaryingParameterSlots()
    {
        var module = PhiConditionalModule();
        var function = Assert.Single(module.FunctionDefinitions.Keys);
        var body = module.GetBody(function);
        var entry = body.Entry;
        var condition = Assert.Single(body[entry].Body.Elements).Result!;
        var varying = ShaderValue.Intermediate(ShaderType.Bool);
        var literalTrue = ShaderValue.Literal(new BoolLiteral(true));
        var load = Instruction<IShaderValue, IShaderValue>.Create(
            new LoadOperation(),
            varying,
            [function.Parameters[0].Value]);
        var uniform = Instruction<IShaderValue, IShaderValue>.Create(
            LogicalBinaryOperation<LogicalAnd>.Instance,
            condition,
            [literalTrue, literalTrue]);
        var join = body.Labels.Single(label => label.Name == "join");
        var uniformParameter = Assert.Single(body[join].Parameters);
        var varyingParameter = ShaderValue.Intermediate(ShaderType.Bool);
        body = body.MapRegionBody(block =>
        {
            var terminator = block.Body.Last.Select(
                jump => ReferenceEquals(jump.Label, join)
                    ? new RegionJump<IShaderValue>(jump.Label, [.. jump.Arguments, varying])
                    : jump,
                static value => value);
            var elements = ReferenceEquals(block.Label, entry)
                ? [load, uniform]
                : block.Body.Elements;
            return block with
            {
                Parameters = ReferenceEquals(block.Label, join)
                    ? [.. block.Parameters, varyingParameter]
                    : block.Parameters,
                Body = Seq.Create(elements, terminator)
            };
        });
        module = new(
            module.Declarations,
            module.FunctionDefinitions.SetItem(function, body));
        var prepared = PrepareTarget(module);
        var targetBody = prepared.Target.GetBody(function);
        var uniformSlot = targetBody.Origins.ParameterSlots[uniformParameter];
        var varyingSlot = targetBody.Origins.ParameterSlots[varyingParameter];
        SlangPlace ReplacePlace(SlangPlace place) =>
            place is SlangVariablePlace variable && ReferenceEquals(variable.Variable, varyingSlot)
                ? new SlangVariablePlace(uniformSlot)
                : place;
        SlangOperand ReplaceOperand(SlangOperand operand) =>
            operand is SlangPlaceOperand place
                ? new SlangPlaceOperand(ReplacePlace(place.Place))
                : operand;
        var aliased = Rewrite(targetBody, statement => statement switch
        {
            SlangBind bind => new SlangBind(
                bind.Instruction.Select(ReplaceOperand, static value => value)),
            SlangAssign assignment => new SlangAssign(
                ReplacePlace(assignment.Target),
                ReplaceOperand(assignment.Value)),
            _ => statement
        });
        aliased = new SlangFunctionBody(
            aliased.Declaration,
            aliased.Body,
            aliased.Origins with
            {
                ParameterSlots = aliased.Origins.ParameterSlots.SetItem(varyingParameter, uniformSlot),
                Parameters = [.. aliased.Origins.Parameters.Select(origin =>
                    ReferenceEquals(origin.Slot, varyingSlot)
                        ? origin with { Slot = uniformSlot }
                        : origin)],
                Transfers = [.. aliased.Origins.Transfers.Select(origin => origin with
                {
                    Arguments = [.. origin.Arguments.Select(argument =>
                        ReferenceEquals(argument.Slot, varyingSlot)
                            ? argument with { Slot = uniformSlot }
                            : argument)]
                })]
            });
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(function, aliased));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("slots are not injective", error.Message);
    }

    [Fact]
    public void RelevantDerivativeMustKeepExactCalleeAndSourceScope()
    {
        var prepared = PrepareTarget(PhiConditionalModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var derivative = body.Origins.Instructions.Single(origin =>
            Equals(origin.Source.Payload, "phi-derivative"));
        var binding = Assert.IsType<SlangBind>(derivative.Target);
        var operands = binding.Instruction.Operands.ToImmutableArray();
        var sin = ShaderFunction.Instance.GetFunction("sin", ShaderType.F32, ShaderType.F32);
        var changedCall = new SlangBind(
            Instruction<SlangOperand, IShaderValue>.Create(
                binding.Instruction.Operation,
                binding.Instruction.Result,
                [new SlangValueOperand(sin), operands[1]],
                binding.Instruction.Payload));
        var wrongBuiltin = Rewrite(
            body,
            statement => ReferenceEquals(statement, binding) ? changedCall : statement);
        var wrongBuiltinModule = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, wrongBuiltin));

        var calleeError = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                wrongBuiltinModule,
                prepared.Facts));

        var removed = Rewrite(
            body,
            statement => ReferenceEquals(statement, binding) ? null : statement);
        var hoisted = new SlangFunctionBody(
            removed.Declaration,
            new SlangBlock([binding, .. removed.Body.Statements]),
            removed.Origins);
        var hoistedModule = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, hoisted));
        var placementError = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                hoistedModule,
                prepared.Facts));

        Assert.Contains("changed its source operation/result/operand lineage", calleeError.Message);
        AssertVerifierRejection(placementError, "moved outside source-label scope");
    }

    [Fact]
    public void TargetVerifierRejectsUnaccountedEarlyReturn()
    {
        var prepared = PrepareTarget(new UniformIgnoringArgumentShader());
        var helper = prepared.Target.FunctionDefinitions.Keys.Single(function =>
            function.Name == nameof(UniformIgnoringArgumentShader.Always));
        var body = prepared.Target.GetBody(helper);
        var loaded = ShaderValue.Intermediate(ShaderType.F32);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var load = new SlangBind(Instruction<SlangOperand, IShaderValue>.Create(
            new LoadOperation(),
            loaded,
            [new SlangPlaceOperand(new SlangParameterPlace(helper.Parameters[0]))]));
        var comparison = new SlangBind(Instruction<SlangOperand, IShaderValue>.Create(
            NumericBinaryRelationalOperation<FloatType<N32>, BinaryRelational.Gt>.Instance,
            condition,
            [
                new SlangValueOperand(loaded),
                new SlangValueOperand(ShaderValue.Literal(new F32Literal(0.0f)))
            ]));
        var corruptedBody = new SlangFunctionBody(
            helper,
            new SlangBlock(
            [
                load,
                comparison,
                new SlangReturnValue(new SlangValueOperand(condition)),
                .. body.Body.Statements
            ]),
            body.Origins);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(helper, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "unaccounted return");
    }

    [Fact]
    public void TargetVerifierRejectsUnaccountedCarrierBreak()
    {
        var prepared = PrepareTarget(PhiConditionalModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var corruptedBody = new SlangFunctionBody(
            body.Declaration,
            new SlangBlock(
            [
                new SlangDoOnce(new SlangBlock([(SlangStatement)new SlangBreak()])),
                .. body.Body.Statements
            ]),
            body.Origins);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "unaccounted break");
    }

    [Fact]
    public void TargetVerifierRejectsCaptureWriteClaimedByWrongDefinition()
    {
        var fixture = CaptureAuthorizationModule();
        var prepared = PrepareTarget(fixture.Module);
        var body = prepared.Target.GetBody(prepared.Function);
        var definition = body.Origins.Definitions.Single(origin =>
            ReferenceEquals(origin.Source.Result, fixture.VaryingComputation));
        Assert.Null(definition.Capture);
        var capture = body.Origins.Captures[fixture.UniformCondition];
        var overwrite = new SlangAssign(
            new SlangVariablePlace(capture),
            new SlangValueOperand(fixture.VaryingComputation));
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements => InsertAfter(statements, definition.Definition, overwrite));
        corruptedBody = new SlangFunctionBody(
            corruptedBody.Declaration,
            corruptedBody.Body,
            corruptedBody.Origins with
            {
                Definitions = [.. corruptedBody.Origins.Definitions.Select(origin =>
                    ReferenceEquals(origin.Source.Result, fixture.VaryingComputation)
                        ? origin with { Capture = overwrite }
                        : origin)]
            });
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("unexpected capture assignment", error.Message);
    }

    [Fact]
    public void TargetVerifierRejectsUnaccountedDerivativeClone()
    {
        var prepared = PrepareTarget(PhiConditionalModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var derivative = Assert.IsType<SlangBind>(
            body.Origins.Instructions.Single(origin =>
                Equals(origin.Source.Payload, "phi-derivative")).Target);
        var clone = new SlangBind(derivative.Instruction);
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements => InsertAfter(statements, derivative, clone));
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "unaccounted target call");
    }

    [Fact]
    public void SourceStoreCannotTargetProjectedUniformCapture()
    {
        var fixture = ProjectedCaptureStoreModule();
        var prepared = PrepareTarget(fixture.Module);
        var body = prepared.Target.GetBody(prepared.Function);
        var capture = body.Origins.Captures[fixture.UniformVector];
        var store = body.Origins.Instructions.Single(origin =>
            origin.Source.Operation is StoreOperation);
        var assignment = Assert.IsType<SlangAssign>(store.Target);
        var corruptedAssignment = new SlangAssign(
            new SlangComponentPlace(
                new SlangVariablePlace(capture),
                "x",
                ShaderType.F32),
            assignment.Value);
        var corruptedBody = Rewrite(
            body,
            statement => ReferenceEquals(statement, assignment)
                ? corruptedAssignment
                : statement);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("source store/setter changed its typed target/value lineage", error.Message);
    }

    [Fact]
    public void AccountedReturnMustRemainTerminalInSourceScope()
    {
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var result = ShaderValue.Intermediate(ShaderType.F32);
        var module = DerivativeCallModule(
            new CallOperation((FunctionType)dpdx.Type),
            result,
            dpdx,
            [ShaderValue.Literal(new F32Literal(1.0f))]);
        var prepared = PrepareTarget(module);
        var body = prepared.Target.GetBody(prepared.Function);
        var derivative = Assert.IsType<SlangBind>(
            body.Origins.Instructions.Single(origin =>
                ReferenceEquals(origin.Source.Result, result)).Target);
        var returned = body.Origins.Returns.Single().Return;
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements => MoveAfter(statements, derivative, returned));
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "return is not terminal");
    }

    [Fact]
    public void SourceConditionalMustRemainTerminalInSourceScope()
    {
        var prepared = PrepareTarget(AddEntryNop(PhiConditionalModule()));
        var body = prepared.Target.GetBody(prepared.Function);
        var nop = body.Origins.Instructions.Single(origin =>
            ReferenceEquals(origin.Label, prepared.Pointer.GetBody(prepared.Function).Entry) &&
            origin.Source.Operation is NopOperation).Target;
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements =>
            {
                var rewrittenConditional = statements.OfType<SlangIf>().SingleOrDefault();
                return rewrittenConditional is null
                    ? statements
                    : MoveAfter(statements, nop, rewrittenConditional);
            });
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "conditional is not terminal");
    }

    [Fact]
    public void SourceUnconditionalTransferMustRemainTerminalInSourceScope()
    {
        var fixture = CaptureAuthorizationModule();
        var prepared = PrepareTarget(AddEntryNop(fixture.Module));
        var body = prepared.Target.GetBody(prepared.Function);
        var entry = prepared.Pointer.GetBody(prepared.Function).Entry;
        var transfer = body.Origins.Transfers.Single(origin =>
            ReferenceEquals(origin.Source, entry));
        var nop = body.Origins.Instructions.Single(origin =>
            ReferenceEquals(origin.Label, entry) &&
            origin.Source.Operation is NopOperation).Target;
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements => MoveAfter(statements, nop, transfer.Break));
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "transfer is not terminal");
    }

    [Fact]
    public void ContinuationGateMustRemainInSourceDerivedActivationSchedule()
    {
        var prepared = PrepareTarget(PhiConditionalModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var moved = body.Origins.Gates.Single(origin =>
            origin.Continuation.Target.Name == "derivative");
        var right = body.Origins.Gates.Single(origin =>
            origin.Continuation.Target.Name == "right");
        var removed = Rewrite(
            body,
            statement => ReferenceEquals(statement, moved.Comparison) ||
                         ReferenceEquals(statement, moved.Conditional)
                ? null
                : statement);
        var destination = removed.Origins.Gates.Single(origin =>
            ReferenceEquals(origin.Continuation.Target, right.Continuation.Target));
        var relocated = Rewrite(
            removed,
            statement => ReferenceEquals(statement, destination.Conditional)
                ? destination.Conditional with
                {
                    WhenTrue = new SlangBlock(
                    [
                        .. destination.Conditional.WhenTrue.Statements,
                        moved.Comparison,
                        moved.Conditional
                    ])
                }
                : statement);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, relocated));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("activation template", error.Message);
    }

    [Fact]
    public void StaleFactsRejectRedirectedZeroArgumentForwardEdges()
    {
        var fixture = CreateZeroArgumentForwardFixture();
        var facts = CLSLCooperationAnalysis.Analyze(fixture.Module);
        var pointer = fixture.Module.RunPass(new StablePointerRegionParameterPass());
        var sourceBody = pointer.GetBody(fixture.Function);
        var changedBody = sourceBody.MapRegionBody(block =>
        {
            if (ReferenceEquals(block.Label, fixture.Left))
                return block with
                {
                    Body = Seq.Create(
                        block.Body.Elements,
                        Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                            new(fixture.Returned, [])))
                };
            if (ReferenceEquals(block.Label, fixture.Right))
                return block with
                {
                    Body = Seq.Create(
                        block.Body.Elements,
                        Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                            new(fixture.Derivative, [])))
                };
            return block;
        });
        var changedPointer = new ShaderModuleDeclaration<RegionFunctionBody>(
            pointer.Declarations,
            pointer.FunctionDefinitions.SetItem(fixture.Function, changedBody));
        var changedTarget = new SlangTargetLowering().Lower(changedPointer);

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                changedPointer,
                changedTarget,
                facts));

        Assert.Contains("original transfer", error.Message);
    }

    [Fact]
    public void StablePointerErasureRetainsFollowingUniformScalarBinding()
    {
        var fixture = CreateMixedPointerUniformBindingFixture();
        var prepared = PrepareTarget(fixture.Module);
        var sourceBody = fixture.Module.GetBody(fixture.Function);
        var pointerBody = prepared.Pointer.GetBody(fixture.Function);
        var sourceParameters = sourceBody[fixture.Branch].Parameters;
        var pointerParameters = pointerBody[fixture.Branch].Parameters;
        var sourceJump = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            sourceBody[sourceBody.Entry].Body.Last).Target;
        var pointerJump = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            pointerBody[pointerBody.Entry].Body.Last).Target;

        Assert.Equal(2, sourceParameters.Length);
        Assert.Same(fixture.UniformCondition, sourceParameters[1]);
        Assert.Equal(2, sourceJump.Arguments.Length);
        Assert.Single(pointerParameters);
        Assert.Same(fixture.UniformCondition, pointerParameters[0]);
        Assert.Single(pointerJump.Arguments);
        Assert.Same(sourceJump.Arguments[1], pointerJump.Arguments[0]);
    }

    [Fact]
    public void SiblingGatesMustRemainInSourceDerivedOrder()
    {
        var prepared = PrepareTarget(PhiConditionalModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var left = body.Origins.Gates.Single(origin =>
            origin.Continuation.Target.Name == "left");
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements => MoveGateBeforeCarrier(statements, left));
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("activation template", error.Message);
    }

    [Fact]
    public void UngatedContinuationMustRemainInSourceDerivedPosition()
    {
        var prepared = PrepareTarget(PhiConditionalModule());
        var body = prepared.Target.GetBody(prepared.Function);
        var join = prepared.Pointer.GetBody(prepared.Function).Labels.Single(label => label.Name == "join");
        var rawJoin = FindDirectCarrier(body.Body, join);
        var left = body.Origins.Gates.Single(origin =>
            origin.Continuation.Target.Name == "left");
        var removed = Rewrite(
            body,
            statement => ReferenceEquals(statement, rawJoin) ? null : statement);
        var destination = removed.Origins.Gates.Single(origin =>
            ReferenceEquals(origin.Continuation.Target, left.Continuation.Target));
        var relocated = Rewrite(
            removed,
            statement => ReferenceEquals(statement, destination.Conditional)
                ? destination.Conditional with
                {
                    WhenTrue = new SlangBlock(
                    [
                        .. destination.Conditional.WhenTrue.Statements,
                        rawJoin
                    ])
                }
                : statement);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Function, relocated));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("activation template", error.Message);
    }

    [Fact]
    public void TargetVerifierRejectsUnaccountedHelperCallClone()
    {
        var prepared = PrepareTarget(new UniformConditionalDerivativeShader());
        var entry = prepared.Target.FunctionDefinitions.Keys.Single(function =>
            function.Name == nameof(UniformConditionalDerivativeShader.Fragment));
        var body = prepared.Target.GetBody(entry);
        var helperCall = Assert.IsType<SlangBind>(
            body.Origins.Instructions.Single(origin =>
                origin.Source.Operation is CallOperation &&
                origin.Source.OperandCount > 0 &&
                origin.Source[0] is FunctionDeclaration callee &&
                callee.Name == nameof(UniformConditionalDerivativeShader.UniformChoice)).Target);
        var clone = new SlangBind(helperCall.Instruction);
        var corruptedBody = Rewrite(
            body,
            static statement => statement,
            statements => InsertAfter(statements, helperCall, clone));
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(entry, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        AssertVerifierRejection(error, "unaccounted target call");
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
    [InlineData(typeof(DerivativeConditionShader), "conditional control")]
    [InlineData(typeof(UniformStorageConditionalShader), "conditional control")]
    [InlineData(typeof(ReadonlyBufferLengthConditionalShader), "conditional control")]
    [InlineData(typeof(ReadonlyBufferLoadConditionalShader), "conditional control")]
    [InlineData(typeof(TextureSampleConditionalDerivativeShader), "conditional control")]
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

    private static CooperationUniformDependencies.Known AssertKnown(
        CooperationUniformDependencies dependencies) =>
        Assert.IsType<CooperationUniformDependencies.Known>(dependencies);

    private static string Format(CooperationUniformDependencies dependencies) =>
        dependencies is CooperationUniformDependencies.Known known
            ? $"[{string.Join(",", known.FormalParameterPositions)}]"
            : "unknown";

    private static IEnumerable<Instruction<IShaderValue, IShaderValue>> Instructions(RegionFunctionBody body)
    {
        var instructions = new List<Instruction<IShaderValue, IShaderValue>>();
        body.Body.Traverse((_, _, block) =>
        {
            instructions.AddRange(block.Body.Elements);
            return false;
        });
        return instructions;
    }

    private static bool AreInOrder(
        SlangBlock block,
        SlangStatement first,
        SlangStatement second)
    {
        var firstIndex = ReferenceIndex(block.Statements, first);
        var secondIndex = ReferenceIndex(block.Statements, second);
        if (firstIndex >= 0 && secondIndex > firstIndex)
            return true;
        return block.Statements.Any(statement => statement switch
        {
            SlangScope scope => AreInOrder(scope.Body, first, second),
            SlangIf conditional =>
                AreInOrder(conditional.WhenTrue, first, second) ||
                AreInOrder(conditional.WhenFalse, first, second),
            SlangDoOnce once => AreInOrder(once.Body, first, second),
            SlangLoop loop => AreInOrder(loop.Body, first, second),
            _ => false
        });
    }

    private static bool AreAdjacent(
        SlangBlock block,
        SlangStatement first,
        SlangStatement second)
    {
        var firstIndex = ReferenceIndex(block.Statements, first);
        var secondIndex = ReferenceIndex(block.Statements, second);
        if (firstIndex >= 0 && secondIndex == firstIndex + 1)
            return true;
        return block.Statements.Any(statement => statement switch
        {
            SlangScope scope => AreAdjacent(scope.Body, first, second),
            SlangIf conditional =>
                AreAdjacent(conditional.WhenTrue, first, second) ||
                AreAdjacent(conditional.WhenFalse, first, second),
            SlangDoOnce once => AreAdjacent(once.Body, first, second),
            SlangLoop loop => AreAdjacent(loop.Body, first, second),
            _ => false
        });
    }

    private static CLSLCooperationFacts Analyze(ISharpShader shader)
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        return CLSLCooperationAnalysis.Analyze(
            CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass()));
    }

    private static void AssertVerifierRejection(NotSupportedException error, string expected)
    {
        Assert.True(
            error.Message.Contains(expected, StringComparison.Ordinal) ||
            error.Message.Contains("activation template", StringComparison.Ordinal) ||
            error.Message.Contains("source-derived", StringComparison.Ordinal),
            $"Expected '{expected}' or activation-template rejection, got: {error.Message}");
    }

    private static PreparedTarget PrepareTarget(ShaderModuleDeclaration<RegionFunctionBody> source)
    {
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        return new(pointer, target, facts, Assert.Single(source.FunctionDefinitions.Keys));
    }

    private static PreparedTarget PrepareTarget(ISharpShader shader)
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        return new(pointer, target, facts, Assert.Single(
            source.FunctionDefinitions.Keys,
            function => function.Attributes.Any(attribute => attribute is FragmentAttribute)));
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
            Dimensions = [.. source.Origins.Dimensions.Select(origin => origin with
            {
                Dimensions = Replace(origin.Dimensions),
                Capture = ReplaceOptional(origin.Capture)
            })],
            Instructions = [.. source.Origins.Instructions.Select(origin => origin with
            {
                Target = Replace(origin.Target)
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
            })],
            Returns = [.. source.Origins.Returns.Select(origin => origin with
            {
                Return = Replace(origin.Return)
            })],
            CarrierBreaks = [.. source.Origins.CarrierBreaks.Select(origin => origin with
            {
                Carrier = Replace(origin.Carrier),
                Break = Replace(origin.Break)
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

    private static ImmutableArray<SlangStatement> InsertAfter(
        ImmutableArray<SlangStatement> statements,
        SlangStatement previous,
        SlangStatement inserted)
    {
        var index = ReferenceIndex(statements, previous);
        return index < 0
            ? statements
            : statements.Insert(index + 1, inserted);
    }

    private static ImmutableArray<SlangStatement> MoveGateBeforeCarrier(
        ImmutableArray<SlangStatement> statements,
        SlangGateOrigin gate)
    {
        var comparison = ReferenceIndex(statements, gate.Comparison);
        var rewrittenConditional = statements.OfType<SlangIf>().SingleOrDefault(statement =>
            Equals(statement.Condition, gate.Conditional.Condition));
        var conditional = rewrittenConditional is null
            ? -1
            : ReferenceIndex(statements, rewrittenConditional);
        if (comparison <= 0 || conditional != comparison + 1 ||
            statements[comparison - 1] is not SlangDoOnce carrier)
            return statements;
        var builder = statements.ToBuilder();
        builder[comparison - 1] = gate.Comparison;
        builder[comparison] = rewrittenConditional!;
        builder[conditional] = carrier;
        return builder.ToImmutable();
    }

    private static SlangDoOnce FindDirectCarrier(SlangBlock block, Label label) =>
        FindDirectCarrierOrNull(block, label) ??
        throw new InvalidOperationException("Could not locate raw source carrier.");

    private static SlangDoOnce? FindDirectCarrierOrNull(SlangBlock block, Label label)
    {
        foreach (var statement in block.Statements)
        {
            if (statement is SlangDoOnce once &&
                once.Body.Statements.Any(child =>
                    child is SlangScope scope && ReferenceEquals(scope.OriginalLabel, label)))
                return once;
            var nested = statement switch
            {
                SlangScope parent => FindDirectCarrierOrNull(parent.Body, label),
                SlangDoOnce parent => FindDirectCarrierOrNull(parent.Body, label),
                SlangIf conditional => FindDirectCarrierOrNull(conditional.WhenTrue, label) ??
                                       FindDirectCarrierOrNull(conditional.WhenFalse, label),
                SlangLoop loop => FindDirectCarrierOrNull(loop.Body, label),
                _ => null
            };
            if (nested is not null)
                return nested;
        }
        return null;
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

    private sealed record CaptureAuthorizationFixture(
        ShaderModuleDeclaration<RegionFunctionBody> Module,
        IShaderValue UniformCondition,
        IShaderValue VaryingComputation);

    private sealed record ProjectedCaptureStoreFixture(
        ShaderModuleDeclaration<RegionFunctionBody> Module,
        IShaderValue UniformVector);

    private sealed record ZeroArgumentForwardFixture(
        ShaderModuleDeclaration<RegionFunctionBody> Module,
        FunctionDeclaration Function,
        Label Left,
        Label Right,
        Label Derivative,
        Label Returned);

    private sealed record MixedPointerUniformBindingFixture(
        ShaderModuleDeclaration<RegionFunctionBody> Module,
        FunctionDeclaration Function,
        Label Branch,
        IShaderValue UniformCondition);

    private sealed record UnusedGlobalPointerFixture(
        ShaderModuleDeclaration<RegionFunctionBody> Module,
        FunctionDeclaration Function,
        VariableDeclaration Storage,
        Label Body);

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

    private static ShaderModuleDeclaration<RegionFunctionBody> DimensionsCaptureModule()
    {
        var input = new VariableDeclaration(
            StorageAddressSpace.Instance,
            "Input",
            ReadOnlyStructuredBufferType<FloatType<DualDrill.Common.Nat.N32>>.Instance,
            [new GroupAttribute(0), new BindingAttribute(0)]);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var returned = Label.Create("returned");
        var count = ShaderValue.Intermediate(ShaderType.U32);
        var value = ShaderValue.Intermediate(ShaderType.F32);
        var derivative = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    StructuredBufferLengthOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                    count,
                    [input.Value],
                    "captured-length")
            ],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(returned, [])));
        var returnBody = RegionFixture.Body(
            returned,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    StructuredBufferLoadOperation<FloatType<DualDrill.Common.Nat.N32>>.Instance,
                    value,
                    [input.Value, count],
                    "captured-length-load"),
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivative,
                    [dpdx, value],
                    "captured-length-derivative")
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivative));
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(returned, [], returnBody, null)],
                entryBody,
                returned));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [input, declaration],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body));
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> TextureCaptureModule()
    {
        var texture = new VariableDeclaration(
            HandleAddressSpace.Instance,
            "Color",
            SampledTexture2DF32Type.Instance,
            [new GroupAttribute(0), new BindingAttribute(2)]);
        var sampler = new VariableDeclaration(
            HandleAddressSpace.Instance,
            "Linear",
            SamplerStateType.Instance,
            [new GroupAttribute(0), new BindingAttribute(3)]);
        var uv = new ParameterDeclaration("uv", ShaderType.Vec2F32, [new LocationAttribute(0)]);
        var lod = new ParameterDeclaration("lod", ShaderType.F32, [new LocationAttribute(1)]);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [uv, lod],
            new FunctionReturn(ShaderType.Vec4F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var returned = Label.Create("returned");
        var uvValue = ShaderValue.Intermediate(ShaderType.Vec2F32);
        var lodValue = ShaderValue.Intermediate(ShaderType.F32);
        var sampled = ShaderValue.Intermediate(ShaderType.Vec4F32);
        var derivative = ShaderValue.Intermediate(ShaderType.Vec4F32);
        var dpdx = ShaderFunction.Instance.GetFunction(
            "dpdx",
            ShaderType.Vec4F32,
            ShaderType.Vec4F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [
                Instruction.Factory.Load(default, new LoadOperation(), uvValue, uv.Value),
                Instruction.Factory.Load(default, new LoadOperation(), lodValue, lod.Value),
                Instruction<IShaderValue, IShaderValue>.Create(
                    TextureSampleLevelOperation.Instance,
                    sampled,
                    [texture.Value, sampler.Value, uvValue, lodValue],
                    "captured-sample")
            ],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(returned, [])));
        var returnBody = RegionFixture.Body(
            returned,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivative,
                    [dpdx, sampled],
                    "captured-sample-derivative")
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivative));
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(returned, [], returnBody, null)],
                entryBody,
                returned));
        return new ShaderModuleDeclaration<RegionFunctionBody>(
            [texture, sampler, declaration],
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

    private static CaptureAuthorizationFixture CaptureAuthorizationModule()
    {
        var varyingParameter = new ParameterDeclaration("varying", ShaderType.Bool, []);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [varyingParameter],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var next = Label.Create("next");
        var derivative = Label.Create("derivative");
        var returned = Label.Create("returned");
        var varying = ShaderValue.Intermediate(ShaderType.Bool);
        var uniform = ShaderValue.Intermediate(ShaderType.Bool);
        var varyingComputation = ShaderValue.Intermediate(ShaderType.Bool);
        var derivativeValue = ShaderValue.Intermediate(ShaderType.F32);
        var literalTrue = ShaderValue.Literal(new BoolLiteral(true));
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new LoadOperation(),
                    varying,
                    [varyingParameter.Value]),
                Instruction<IShaderValue, IShaderValue>.Create(
                    new LiteralOperation(),
                    uniform,
                    [literalTrue])
            ],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(next, [])));
        var nextBody = RegionFixture.Body(
            next,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    LogicalBinaryOperation<LogicalAnd>.Instance,
                    varyingComputation,
                    [varying, literalTrue])
            ],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                uniform,
                new(derivative, []),
                new(returned, [])));
        var derivativeBody = RegionFixture.Body(
            derivative,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivativeValue,
                    [dpdx, ShaderValue.Literal(new F32Literal(1.0f))],
                    "capture-derivative")
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
                    RegionTree.Block(derivative, [], derivativeBody, null),
                    RegionTree.Block(next, [], nextBody, null)
                ],
                entryBody,
                next));
        return new(
            new ShaderModuleDeclaration<RegionFunctionBody>(
                [declaration],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body)),
            uniform,
            varyingComputation);
    }

    private static ProjectedCaptureStoreFixture ProjectedCaptureStoreModule()
    {
        var varyingParameter = new ParameterDeclaration("varying", ShaderType.F32, []);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [varyingParameter],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var local = new VariableDeclaration(
            FunctionAddressSpace.Instance,
            "local",
            ShaderType.F32,
            []);
        var entry = Label.Create("entry");
        var next = Label.Create("next");
        var derivative = Label.Create("derivative");
        var returned = Label.Create("returned");
        var uniformVector = ShaderValue.Intermediate(ShaderType.Vec2F32);
        var varying = ShaderValue.Intermediate(ShaderType.F32);
        var norm = ShaderValue.Intermediate(ShaderType.F32);
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var derivativeValue = ShaderValue.Intermediate(ShaderType.F32);
        var dot = ShaderFunction.Instance.GetFunction(
            "dot",
            ShaderType.F32,
            ShaderType.Vec2F32,
            ShaderType.Vec2F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    VectorCompositeConstructionOperation.Get(
                        (IVecType)ShaderType.Vec2F32,
                        [ShaderType.F32, ShaderType.F32]),
                    uniformVector,
                    [
                        ShaderValue.Literal(new F32Literal(1.0f)),
                        ShaderValue.Literal(new F32Literal(0.0f))
                    ]),
                Instruction<IShaderValue, IShaderValue>.Create(
                    new LoadOperation(),
                    varying,
                    [varyingParameter.Value])
            ],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(next, [])));
        var nextBody = RegionFixture.Body(
            next,
            [],
            [
                Instruction.Factory.Store(default, new StoreOperation(), local.Value, varying),
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dot.Type),
                    norm,
                    [dot, uniformVector, uniformVector]),
                Instruction<IShaderValue, IShaderValue>.Create(
                    NumericBinaryRelationalOperation<FloatType<N32>, BinaryRelational.Gt>.Instance,
                    condition,
                    [norm, ShaderValue.Literal(new F32Literal(0.5f))])
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
                    RegionTree.Block(derivative, [], derivativeBody, null),
                    RegionTree.Block(next, [], nextBody, null)
                ],
                entryBody,
                next));
        return new(
            new ShaderModuleDeclaration<RegionFunctionBody>(
                [declaration, local],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body)),
            uniformVector);
    }

    private static ZeroArgumentForwardFixture CreateZeroArgumentForwardFixture()
    {
        var declaration = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var left = Label.Create("left");
        var right = Label.Create("right");
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
                    new LiteralOperation(),
                    condition,
                    [ShaderValue.Literal(new BoolLiteral(true))])
            ],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition,
                new(left, []),
                new(right, [])));
        var leftBody = RegionFixture.Body(
            left,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(derivative, [])));
        var rightBody = RegionFixture.Body(
            right,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(returned, [])));
        var derivativeBody = RegionFixture.Body(
            derivative,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivativeValue,
                    [dpdx, ShaderValue.Literal(new F32Literal(1.0f))],
                    "redirect-derivative")
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
                    RegionTree.Block(derivative, [], derivativeBody, null),
                    RegionTree.Block(right, [], rightBody, null),
                    RegionTree.Block(left, [], leftBody, null)
                ],
                entryBody,
                null));
        return new(
            new ShaderModuleDeclaration<RegionFunctionBody>(
                [declaration],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body)),
            declaration,
            left,
            right,
            derivative,
            returned);
    }

    private static MixedPointerUniformBindingFixture CreateMixedPointerUniformBindingFixture()
    {
        var varyingParameter = new ParameterDeclaration("varying", ShaderType.F32, []);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [varyingParameter],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var branch = Label.Create("branch");
        var derivative = Label.Create("derivative");
        var returned = Label.Create("returned");
        var pointerParameter = ShaderValue.Intermediate(varyingParameter.Value.Type);
        var uniformCondition = ShaderValue.Intermediate(ShaderType.Bool);
        var varying = ShaderValue.Intermediate(ShaderType.F32);
        var derivativeValue = ShaderValue.Intermediate(ShaderType.F32);
        var literalTrue = ShaderValue.Literal(new BoolLiteral(true));
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                new(branch, [varyingParameter.Value, literalTrue])));
        var branchBody = RegionFixture.Body(
            branch,
            [pointerParameter, uniformCondition],
            [],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                uniformCondition,
                new(derivative, []),
                new(returned, [])));
        var derivativeBody = RegionFixture.Body(
            derivative,
            [],
            [
                Instruction.Factory.Load(
                    default,
                    new LoadOperation(),
                    varying,
                    varyingParameter.Value),
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivativeValue,
                    [dpdx, varying],
                    "mixed-pointer-derivative")
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
                    RegionTree.Block(derivative, [], derivativeBody, null),
                    RegionTree.Block(branch, [], branchBody, null)
                ],
                entryBody,
                branch));
        return new(
            new ShaderModuleDeclaration<RegionFunctionBody>(
                [declaration],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body)),
            declaration,
            branch,
            uniformCondition);
    }

    private static UnusedGlobalPointerFixture CreateUnusedGlobalPointerFixture()
    {
        var structure = new StructureDeclaration
        {
            Name = "UnusedGlobals",
            Members = [new MemberDeclaration("value", ShaderType.F32, [])]
        };
        var structureType = new StructureType(structure);
        var storage = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "unusedGlobals",
            structureType,
            [
                new UniformAttribute(),
                new GroupAttribute(0),
                new BindingAttribute(0)
            ]);
        var declaration = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entry = Label.Create("entry");
        var bodyLabel = Label.Create("body");
        var pointerParameter = ShaderValue.Intermediate(storage.Value.Type);
        var derivative = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entry,
            [],
            [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                new(bodyLabel, [storage.Value])));
        var terminalBody = RegionFixture.Body(
            bodyLabel,
            [pointerParameter],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivative,
                    [dpdx, ShaderValue.Literal(new F32Literal(1.0f))],
                    "unused-global-pointer-derivative")
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivative));
        var body = RegionFixture.CreateFunctionBody(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(bodyLabel, [], terminalBody, null)],
                entryBody,
                bodyLabel));
        return new(
            new ShaderModuleDeclaration<RegionFunctionBody>(
                [structure, storage, declaration],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(declaration, body)),
            declaration,
            storage,
            bodyLabel);
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> AddEntryNop(
        ShaderModuleDeclaration<RegionFunctionBody> module)
    {
        var function = Assert.Single(module.FunctionDefinitions.Keys);
        var body = module.GetBody(function);
        var updated = body.MapRegionBody(block =>
            ReferenceEquals(block.Label, body.Entry)
                ? block with
                {
                    Body = Seq.Create(
                        [.. block.Body.Elements,
                         Instruction<IShaderValue, IShaderValue>.Create(NopOperation.Instance, null, [])],
                        block.Body.Last)
                }
                : block);
        return new(module.Declarations, module.FunctionDefinitions.SetItem(function, updated));
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

    private struct CooperationGlobals
    {
        public float value;
    }

    private sealed class UniformStorageDerivativeShader : ISharpShader
    {
        [Uniform]
        [Group(0)]
        [Binding(0)]
        private static readonly CooperationGlobals globals;

        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying) =>
            DMath.dpdx(globals.value + varying);
    }

    private sealed class ReadonlyBufferLoadDerivativeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment() => DMath.dpdx(Input[0u]);
    }

    private sealed class IntegerResourceDerivativeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<int> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment() => DMath.dpdx((float)Input[Input.Length - 1u]);
    }

    private sealed class ReadonlyBufferLengthDerivativeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment() => DMath.dpdx(Input[Input.Length - 1u]);
    }

    private sealed class ReadonlyBufferLengthAcrossBlocksShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment()
        {
            var index = Input.Length - (UniformChoice() ? 1u : Input.Length);
            return DMath.dpdx(Input[index]);
        }

        [ShaderMethod]
        public static bool UniformChoice() => true;
    }

    private sealed class UniformStorageConditionalShader : ISharpShader
    {
        [Uniform]
        [Group(0)]
        [Binding(0)]
        private static readonly CooperationGlobals globals;

        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (globals.value > 0.0f)
                return DMath.dpdx(varying);
            return varying;
        }
    }

    private sealed class ReadonlyBufferLengthConditionalShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment()
        {
            if (Input.Length > 0u)
                return DMath.dpdx(0.0f);
            return 0.0f;
        }
    }

    private sealed class TextureToWritableComputeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static readonly RWStructuredBuffer<float> Output;

        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;
#pragma warning restore CS0649

        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Run()
        {
            if (Output.Length > 0u)
                Output[0u] = Color.SampleLevel(Linear, DMath.vec2(0.5f), 0.0f).x;
        }
    }

    private sealed class TextureSampleDerivativeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment(
            [Location(0)] vec2f32 uv,
            [Location(1)] float lod) =>
            DMath.dpdx(Color.SampleLevel(Linear, uv, lod).x);
    }

    private sealed class CrossLabelTextureSampleDerivativeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment(
            [Location(0)] vec2f32 uv,
            [Location(1)] float lod)
        {
            var sampled = (int)Color.SampleLevel(Linear, uv, lod).x;
            if (UniformChoice())
                return DMath.dpdx((float)sampled);
            return 0.0f;
        }

        [ShaderMethod]
        public static bool UniformChoice() => true;
    }

    private sealed class TextureSampleSensitiveArgumentShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment(
            [Location(0)] vec2f32 uv,
            [Location(1)] float lod) =>
            Color.SampleLevel(Linear, uv, DMath.dpdx(lod)).x;
    }

    private sealed class TextureSampleConditionalDerivativeShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(2)]
        private static readonly Texture2D<float> Color;

        [Group(0), Binding(3)]
        private static readonly SamplerState Linear;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment(
            [Location(0)] vec2f32 uv,
            [Location(1)] float lod)
        {
            var sampled = Color.SampleLevel(Linear, uv, lod).x;
            if (sampled > 0.0f)
                return DMath.dpdx(sampled);
            return sampled;
        }
    }

    private sealed class ReadonlyBufferLoadConditionalShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment()
        {
            if (Input[0u] > 0.0f)
                return DMath.dpdx(0.0f);
            return 0.0f;
        }
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
        [Compute, WorkgroupSize(1, 1, 1)]
        public static void Compute() => _ = DMath.dpdx(0.0f);
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
