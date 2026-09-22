using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Graphics;

namespace DualDrill.CLSL;

public sealed record CooperationCallSite(
    FunctionDeclaration Caller,
    Label Label,
    int InstructionOrdinal,
    FunctionDeclaration Callee,
    object? Payload);

public sealed record CooperationInstructionFact(
    FunctionDeclaration Function,
    Label Label,
    int InstructionOrdinal,
    IOperation Operation,
    IShaderValue? Result,
    ImmutableArray<IShaderValue> Operands,
    object? Payload);

public sealed record EntryUniformQuadParticipation(
    FunctionDeclaration Entry,
    ImmutableDictionary<FunctionDeclaration, ImmutableArray<Label>> OriginalBlocks,
    ImmutableArray<OperationRequirementSite> OriginalSensitiveSites,
    ImmutableArray<CooperationCallSite> CallInheritance,
    ImmutableArray<CooperationInstructionFact> OriginalRelevantInstructions,
    ImmutableDictionary<FunctionDeclaration, CooperationFunctionUniformityFacts> Uniformity);

public sealed record CLSLCooperationFacts(
    ImmutableArray<EntryUniformQuadParticipation> EntryUniformQuadParticipations)
{
    public static CLSLCooperationFacts Empty { get; } = new([]);
}

public static class CLSLCooperationAnalysis
{
    public static CLSLCooperationFacts Analyze(
        ShaderModuleDeclaration<RegionFunctionBody> normalized,
        CLSLCooperationProfile profile = CLSLCooperationProfile.PortableWgsl) =>
        CooperationAdmission.Prepare(normalized, profile).Facts;
}

internal sealed record CooperationPreparation(
    CLSLCooperationFacts Facts,
    ShaderModuleDeclaration<SlangFunctionBody>? Target);

internal static class CooperationAdmission
{
    private const OperationRequirement SensitiveRequirements =
        OperationRequirement.DerivativeQuad |
        OperationRequirement.SubgroupParticipation |
        OperationRequirement.WorkgroupBarrier;

    internal static CooperationPreparation Prepare(
        ShaderModuleDeclaration<RegionFunctionBody> normalized,
        CLSLCooperationProfile profile,
        SlangControlFlowPolicy controlFlowPolicy = SlangControlFlowPolicy.Native)
    {
        var effects = FunctionEffectAnalysis.Analyze(normalized);
        return profile switch
        {
            CLSLCooperationProfile.Scalar => PrepareScalar(effects),
            CLSLCooperationProfile.PortableWgsl =>
                PreparePortable(normalized, effects, controlFlowPolicy),
            _ => throw new ArgumentOutOfRangeException(
                nameof(profile),
                profile,
                "Unknown CLSL cooperation profile.")
        };
    }

    private static CooperationPreparation PrepareScalar(FunctionEffectAnalysisResult effects)
    {
        var staged = effects.Summaries
            .Select(item => (
                Function: item.Key,
                Stage: Stage(item.Key),
                Site: item.Value.RequirementSites.FirstOrDefault(
                    static site => (site.Requirements & SensitiveRequirements) != 0)))
            .Where(static item => item.Stage is not null && item.Site is not null)
            .OrderBy(static item => item.Function.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (staged.Site is not null)
            throw Error(
                CLSLCooperationProfile.Scalar,
                staged.Stage!.Value.ToString(),
                staged.Site,
                staged.Site.Requirements & SensitiveRequirements,
                "Scalar does not opt out of cooperative-operation legality");
        var unowned = effects.Summaries.Values
            .SelectMany(static summary => summary.RequirementSites)
            .FirstOrDefault(static site => (site.Requirements & SensitiveRequirements) != 0);
        if (unowned is not null)
            throw Error(
                CLSLCooperationProfile.Scalar,
                "unowned",
                unowned,
                unowned.Requirements & SensitiveRequirements,
                "Scalar does not opt out of cooperative-operation legality");
        return new CooperationPreparation(CLSLCooperationFacts.Empty, null);
    }

    private static CooperationPreparation PreparePortable(
        ShaderModuleDeclaration<RegionFunctionBody> normalized,
        FunctionEffectAnalysisResult effects,
        SlangControlFlowPolicy controlFlowPolicy)
    {
        var calls = normalized.FunctionDefinitions.ToImmutableDictionary(
            static item => item.Key,
            item => DirectCalls(item.Key, item.Value, normalized.FunctionDefinitions));
        var entries = normalized.FunctionDefinitions.Keys
            .Select(function => (Function: function, Stage: Stage(function)))
            .Where(static item => item.Stage is not null)
            .ToImmutableArray();
        var owners = normalized.FunctionDefinitions.Keys.ToDictionary(
            static function => function,
            static _ => ImmutableArray.CreateBuilder<(FunctionDeclaration Entry, GPUShaderStage Stage)>());
        foreach (var entry in entries)
        {
            foreach (var function in Closure(entry.Function, calls))
                owners[function].Add((entry.Function, entry.Stage!.Value));
        }

        var incomplete = effects.Summaries
            .OrderBy(static item => item.Key.Name, StringComparer.Ordinal)
            .SelectMany(static item => item.Value.UnknownSites)
            .FirstOrDefault();
        if (incomplete is not null)
            throw new NotSupportedException(
                $"PortableWgsl requires complete effect summaries; function '{incomplete.Function.Name}', " +
                $"block '{incomplete.Label.Name}', operation '{incomplete.Operation.Name}' is " +
                $"{incomplete.Reason}, stage '{StageDescription(owners[incomplete.Function])}'" +
                $"{Provenance(incomplete.Payload)}.");

        foreach (var site in effects.Summaries
            .SelectMany(item => item.Value.RequirementSites.Where(site =>
                ReferenceEquals(site.Function, item.Key) &&
                (site.Requirements & OperationRequirement.DerivativeQuad) != 0)))
            CheckDerivative(
                normalized,
                site,
                StageDescription(owners[site.Function]));
        if (PortableDerivativeTarget.FindUsedNameCollision(normalized) is { } collision)
        {
            var site = effects[collision.Function].RequirementSites.First(requirement =>
                ReferenceEquals(requirement.Function, collision.Function) &&
                ReferenceEquals(requirement.Label, collision.Label) &&
                requirement.InstructionOrdinal == collision.InstructionOrdinal &&
                ReferenceEquals(requirement.Operation, collision.Operation) &&
                (requirement.Requirements & OperationRequirement.DerivativeQuad) != 0);
            throw Error(
                CLSLCooperationProfile.PortableWgsl,
                StageDescription(owners[site.Function]),
                site,
                OperationRequirement.DerivativeQuad,
                $"mapped target spelling '{collision.Target.Name}' collides with module declaration " +
                $"'{collision.ModuleDeclaration.Name}'");
        }

        foreach (var item in effects.Summaries.OrderBy(static item => item.Key.Name, StringComparer.Ordinal))
        {
            foreach (var site in item.Value.RequirementSites.Where(
                         static site => (site.Requirements &
                             (OperationRequirement.SubgroupParticipation |
                              OperationRequirement.WorkgroupBarrier)) != 0))
                throw Error(
                    CLSLCooperationProfile.PortableWgsl,
                    StageDescription(owners[site.Function]),
                    site,
                    site.Requirements &
                    (OperationRequirement.SubgroupParticipation | OperationRequirement.WorkgroupBarrier),
                    "the first portable profile implements derivative-quad participation only");

            var directDerivative = item.Value.RequirementSites.Any(site =>
                ReferenceEquals(site.Function, item.Key) &&
                (site.Requirements & OperationRequirement.DerivativeQuad) != 0);
            if (directDerivative && owners[item.Key].Count == 0)
            {
                var site = item.Value.RequirementSites.First(site =>
                    ReferenceEquals(site.Function, item.Key) &&
                    (site.Requirements & OperationRequirement.DerivativeQuad) != 0);
                throw Error(
                    CLSLCooperationProfile.PortableWgsl,
                    "unowned",
                    site,
                    OperationRequirement.DerivativeQuad,
                    "cooperative definition has no reachable shader-stage entry");
            }
        }

        var participation = ImmutableArray.CreateBuilder<EntryUniformQuadParticipation>();
        foreach (var entry in entries.OrderBy(static item => item.Function.Name, StringComparer.Ordinal))
        {
            var summary = effects[entry.Function];
            if ((summary.Requirements & SensitiveRequirements) == 0)
                continue;

            if (entry.Stage is not GPUShaderStage.Fragment)
            {
                var site = summary.RequirementSites.First(site =>
                    (site.Requirements & SensitiveRequirements) != 0);
                throw Error(
                    CLSLCooperationProfile.PortableWgsl,
                    entry.Stage?.ToString() ?? "unowned",
                    site,
                    site.Requirements & SensitiveRequirements,
                    $"entry '{entry.Function.Name}' is not an unambiguous fragment entry");
            }

            var closure = Closure(entry.Function, calls);
            foreach (var function in closure)
            {
                foreach (var call in calls[function])
                    CheckOrdinaryCall(normalized, entry.Function, call);
                CheckNumericBuiltinCalls(normalized.FunctionDefinitions[function], entry.Function);
            }
            var uniformity = CooperationUniformity.Analyze(
                normalized,
                effects,
                closure,
                calls,
                entry.Function);
            foreach (var builtin in uniformity.DependencyBuiltinCalls)
            {
                var targetName = builtin.Builtin.Name == "mix" ? "lerp" : builtin.Builtin.Name;
                var dependencyCollision = normalized.Declarations
                    .OfType<FunctionDeclaration>()
                    .FirstOrDefault(function =>
                        string.Equals(function.Name, targetName, StringComparison.Ordinal));
                if (dependencyCollision is not null)
                    throw new NotSupportedException(
                        $"PortableWgsl entry '{entry.Function.Name}', function '{builtin.Function.Name}', " +
                        $"block '{builtin.Label.Name}', operation 'call': numeric builtin " +
                        $"'{builtin.Builtin.Name}' supporting a dependency proof maps to target spelling " +
                        $"'{targetName}', which collides with module declaration '{dependencyCollision.Name}'.");
            }

            var callInheritance = closure.SelectMany(function => calls[function]).ToImmutableArray();
            var sensitiveSites = summary.RequirementSites
                .Where(static site => (site.Requirements & SensitiveRequirements) != 0)
                .ToImmutableArray();
            participation.Add(new EntryUniformQuadParticipation(
                entry.Function,
                ImmutableDictionary.CreateRange(
                    ReferenceEqualityComparer.Instance,
                    uniformity.Functions.Select(static item =>
                        KeyValuePair.Create(item.Key, item.Value.OriginalBlocks))),
                sensitiveSites,
                callInheritance,
                RelevantInstructionFacts(normalized, sensitiveSites, callInheritance, closure),
                uniformity.Functions));
        }

        var facts = new CLSLCooperationFacts(participation.ToImmutable());
        if (facts.EntryUniformQuadParticipations.IsEmpty)
            return new CooperationPreparation(facts, null);
        var pointer = normalized.RunPass(new StablePointerRegionParameterPass());
        CheckPointerCorrespondence(normalized, pointer, facts);
        var target = new SlangTargetLowering().Lower(pointer, controlFlowPolicy);
        CheckTargetCorrespondence(pointer, target, facts);
        return new CooperationPreparation(facts, target);
    }

    private static GPUShaderStage? Stage(FunctionDeclaration function)
    {
        var stages = function.Attributes
            .Select(static attribute => attribute switch
            {
                FragmentAttribute => GPUShaderStage.Fragment,
                VertexAttribute => GPUShaderStage.Vertex,
                ComputeAttribute => GPUShaderStage.Compute,
                _ => GPUShaderStage.None
            })
            .Where(static stage => stage != GPUShaderStage.None)
            .Distinct()
            .ToArray();
        if (stages.Length > 1)
            throw new NotSupportedException(
                $"Cooperation entry '{function.Name}' has ambiguous shader stages: " +
                string.Join(", ", stages));
        return stages.Length == 1 ? stages[0] : null;
    }

    internal static ImmutableArray<FunctionDeclaration> Closure(
        FunctionDeclaration entry,
        ImmutableDictionary<FunctionDeclaration, ImmutableArray<CooperationCallSite>> calls)
    {
        var visited = new HashSet<FunctionDeclaration>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<FunctionDeclaration>();
        var result = ImmutableArray.CreateBuilder<FunctionDeclaration>();
        pending.Enqueue(entry);
        while (pending.TryDequeue(out var function))
        {
            if (!visited.Add(function))
                continue;
            result.Add(function);
            foreach (var call in calls[function])
                pending.Enqueue(call.Callee);
        }
        return result.ToImmutable();
    }

    private static string StageDescription(
        IEnumerable<(FunctionDeclaration Entry, GPUShaderStage Stage)> owners)
    {
        var stages = owners.Select(static owner => owner.Stage).Distinct().ToArray();
        return stages.Length == 0 ? "unowned" : string.Join("|", stages);
    }

    internal static ImmutableArray<CooperationCallSite> DirectCalls(
        FunctionDeclaration function,
        RegionFunctionBody body,
        ImmutableDictionary<FunctionDeclaration, RegionFunctionBody> definitions)
    {
        var result = ImmutableArray.CreateBuilder<CooperationCallSite>();
        body.Body.Traverse((_, label, block) =>
        {
            foreach (var (instruction, ordinal) in block.Body.Elements.Select(
                         static (instruction, ordinal) => (instruction, ordinal)))
                if (instruction.Operation is CallOperation &&
                    instruction.OperandCount > 0 &&
                    instruction[0] is FunctionDeclaration callee &&
                    definitions.ContainsKey(callee))
                    result.Add(new(function, label, ordinal, callee, instruction.Payload));
            return false;
        });
        return result.ToImmutable();
    }

    private static ImmutableArray<CooperationInstructionFact> RelevantInstructionFacts(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        ImmutableArray<OperationRequirementSite> sensitiveSites,
        ImmutableArray<CooperationCallSite> calls,
        ImmutableArray<FunctionDeclaration> closure)
    {
        var resourceSites = ImmutableArray.CreateBuilder<(
            FunctionDeclaration Function,
            Label Label,
            int InstructionOrdinal)>();
        foreach (var function in closure)
            module.FunctionDefinitions[function].Body.Traverse((_, label, block) =>
            {
                foreach (var (instruction, ordinal) in block.Body.Elements.Select(
                             static (instruction, ordinal) => (instruction, ordinal)))
                    if (instruction.Operation is
                        StructuredBufferLengthOperation or
                        StructuredBufferLoadOperation or
                        ReadWriteStructuredBufferLengthOperation or
                        ReadWriteStructuredBufferLoadOperation or
                        ReadWriteStructuredBufferStoreOperation or
                        TextureSampleLevelOperation)
                        resourceSites.Add((function, label, ordinal));
                return false;
            });

        return
        [
            .. sensitiveSites
            .Select(static site => (site.Function, site.Label, site.InstructionOrdinal))
            .Concat(calls.Select(static site => (site.Caller, site.Label, site.InstructionOrdinal)))
            .Concat(resourceSites)
            .Distinct()
            .Select(site =>
            {
                var instruction = InstructionAt(
                    module.FunctionDefinitions[site.Item1],
                    site.Label,
                    site.InstructionOrdinal);
                return new CooperationInstructionFact(
                    site.Item1,
                    site.Label,
                    site.InstructionOrdinal,
                    instruction.Operation,
                    instruction.Result,
                    [.. instruction.Operands],
                    instruction.Payload);
            })
        ];
    }

    private static void CheckDerivative(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        OperationRequirementSite site,
        string stage)
    {
        var instruction = InstructionAt(module.FunctionDefinitions[site.Function], site.Label, site.InstructionOrdinal);
        if (instruction.Operation is not CallOperation ||
            instruction.OperandCount == 0 ||
            instruction[0] is not FunctionDeclaration declaration ||
            !PortableDerivativeTarget.TryLower(declaration, out _))
            throw Error(
                CLSLCooperationProfile.PortableWgsl,
                stage,
                site,
                OperationRequirement.DerivativeQuad,
                instruction.OperandCount > 0 && instruction[0] is FunctionDeclaration function
                    ? PortableDerivativeTarget.UnsupportedReason(function)
                    : "derivative requirement is not a supported builtin call");
        CheckCall(
            instruction,
            declaration,
            $"PortableWgsl admission rejected function '{site.Function.Name}', block '{site.Label.Name}', " +
            $"operation '{instruction.Operation.Name}', requirement '{OperationRequirement.DerivativeQuad}', " +
            $"stage '{stage}'{Provenance(instruction.Payload)}");
    }

    private static void CheckOrdinaryCall(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        FunctionDeclaration entry,
        CooperationCallSite site)
    {
        var instruction = InstructionAt(module.FunctionDefinitions[site.Caller], site.Label, site.InstructionOrdinal);
        CheckCall(
            instruction,
            site.Callee,
            $"PortableWgsl entry '{entry.Name}', function '{site.Caller.Name}', block " +
            $"'{site.Label.Name}', operation '{instruction.Operation.Name}'{Provenance(instruction.Payload)}");
    }

    private static void CheckNumericBuiltinCalls(
        RegionFunctionBody body,
        FunctionDeclaration entry)
    {
        body.Body.Traverse((_, label, block) =>
        {
            foreach (var (ordinal, instruction) in block.Body.Elements.Index())
                if (instruction.Operation is CallOperation &&
                    instruction.OperandCount > 0 &&
                    instruction[0] is FunctionDeclaration callee &&
                    ShaderFunction.Instance.Functions.Contains(callee))
                    CheckCall(
                        instruction,
                        callee,
                        $"PortableWgsl entry '{entry.Name}', function '{body.Declaration.Name}', block " +
                        $"'{label.Name}', numeric builtin instruction {ordinal}{Provenance(instruction.Payload)}");
            return false;
        });
    }

    private static void CheckCall(
        Instruction<IShaderValue, IShaderValue> instruction,
        FunctionDeclaration callee,
        string context)
    {
        if (instruction.Operation is not CallOperation call)
            throw new NotSupportedException($"{context}: expected a call operation.");
        var calleeType = (FunctionType)callee.Type;
        if (!call.CalleeType.Equals(calleeType))
            throw new NotSupportedException(
                $"{context}: call operation signature '{call.CalleeType.Name}' does not match " +
                $"callee '{callee.Name}' signature '{calleeType.Name}'.");
        var expectedOperands = callee.Parameters.Length + 1;
        if (instruction.OperandCount != expectedOperands)
            throw new NotSupportedException(
                $"{context}: call to '{callee.Name}' requires {callee.Parameters.Length} arguments, " +
                $"got {Math.Max(0, instruction.OperandCount - 1)}.");
        if (!ReferenceEquals(instruction[0], callee))
            throw new NotSupportedException(
                $"{context}: call callee operand does not preserve declaration identity for '{callee.Name}'.");
        for (var index = 0; index < callee.Parameters.Length; index++)
        {
            var actual = instruction[index + 1]
                ?? throw new NotSupportedException(
                    $"{context}: call argument {index} for '{callee.Name}' is missing.");
            var expected = callee.Parameters[index].Type;
            if (!actual.Type.Equals(expected))
                throw new NotSupportedException(
                    $"{context}: call argument {index} for '{callee.Name}' requires '{expected.Name}', " +
                    $"got '{actual.Type.Name}'.");
        }
        if (callee.ReturnType is UnitType)
        {
            if (instruction.Result is not null && instruction.Result.Type is not UnitType)
                throw new NotSupportedException(
                    $"{context}: Unit call '{callee.Name}' has result type '{instruction.Result.Type.Name}'.");
        }
        else if (instruction.Result is null)
        {
            throw new NotSupportedException(
                $"{context}: call to '{callee.Name}' is missing result '{callee.ReturnType.Name}'.");
        }
        else if (!instruction.Result.Type.Equals(callee.ReturnType))
        {
            throw new NotSupportedException(
                $"{context}: call to '{callee.Name}' requires result '{callee.ReturnType.Name}', " +
                $"got '{instruction.Result.Type.Name}'.");
        }
    }

    private static void CheckPointerCorrespondence(
        ShaderModuleDeclaration<RegionFunctionBody> source,
        ShaderModuleDeclaration<RegionFunctionBody> pointer,
        CLSLCooperationFacts facts)
    {
        foreach (var participation in facts.EntryUniformQuadParticipations)
            foreach (var (function, labels) in participation.OriginalBlocks)
            {
                var sourceBody = source.FunctionDefinitions[function];
                var pointerBody = pointer.FunctionDefinitions[function];
                if (labels.Length != pointerBody.Labels.Length ||
                    !labels.ToHashSet(ReferenceEqualityComparer.Instance)
                        .SetEquals(pointerBody.Labels))
                    throw new NotSupportedException(
                        $"PortableWgsl pointer lowering changed original block identity in " +
                        $"function '{function.Name}' for entry '{participation.Entry.Name}'.");
                foreach (var fact in participation.OriginalRelevantInstructions.Where(
                             fact => ReferenceEquals(fact.Function, function)))
                {
                    var before = InstructionAt(
                        sourceBody,
                        fact.Label,
                        fact.InstructionOrdinal);
                    var after = InstructionAt(pointerBody, fact.Label, fact.InstructionOrdinal);
                    if (!MatchesInstructionFact(before, fact) || !SameInstruction(before, after))
                        throw new NotSupportedException(
                            $"PortableWgsl pointer lowering did not preserve exactly one placement of " +
                            $"function '{function.Name}', block '{fact.Label.Name}', operation " +
                            $"'{before.Operation.Name}'{Provenance(before.Payload)}.");
                }

                var uniformity = participation.Uniformity[function];
                var eligibleFormals = CooperationUniformity.EligibleFormalLoads(sourceBody)
                    .Values.Order().ToImmutableArray();
                if (!eligibleFormals.SequenceEqual(uniformity.EligibleFormalParameterPositions))
                    throw PointerProofError(
                        participation,
                        function,
                        sourceBody.Entry,
                        "eligible formal parameter set changed");
                if (sourceBody.Control.Transfers.Length != uniformity.OriginalTransfers.Length ||
                    pointerBody.Control.Transfers.Length != uniformity.OriginalTransfers.Length)
                    throw PointerProofError(
                        participation,
                        function,
                        sourceBody.Entry,
                        "original transfer count changed");
                foreach (var transfer in uniformity.OriginalTransfers)
                    if (!CooperationUniformity.CooperationTransferFacts.Matches(
                            sourceBody,
                            transfer,
                            allowPointerParameterErasure: false) ||
                        !CooperationUniformity.CooperationTransferFacts.Matches(
                            pointerBody,
                            transfer,
                            allowPointerParameterErasure: true))
                        throw PointerProofError(
                            participation,
                            function,
                            transfer.Source,
                            $"original transfer arm {transfer.Arm} target/owner/kind/arguments changed");

                foreach (var fact in uniformity.DependencyValues)
                {
                    if (fact.Kind is CooperationDependencyValueKind.BlockParameter)
                    {
                        var position = sourceBody[fact.Label].Parameters.IndexOf(fact.Value);
                        if (position < 0)
                            throw PointerProofError(
                                participation,
                                function,
                                fact.Label,
                                "dependency block parameter identity changed");
                        continue;
                    }

                    var ordinal = fact.InstructionOrdinal ??
                        throw PointerProofError(
                            participation,
                            function,
                            fact.Label,
                            "dependency definition has no source instruction");
                    var before = InstructionAt(sourceBody, fact.Label, ordinal);
                    var after = InstructionAt(pointerBody, fact.Label, ordinal);
                    if (!MatchesDependencyFact(before, fact) || !SameInstruction(before, after))
                        throw PointerProofError(
                            participation,
                            function,
                            fact.Label,
                            "proof-relevant dependency definition changed");
                }

                foreach (var binding in uniformity.DependencyBindings)
                {
                    var beforeJump = JumpAt(sourceBody[binding.Source].Body.Last, binding.Arm);
                    if (!ReferenceEquals(beforeJump.Label, binding.Target) ||
                        beforeJump.Arguments.Length <= binding.ParameterPosition ||
                        !ReferenceEquals(
                            beforeJump.Arguments[binding.ParameterPosition],
                            binding.Argument) ||
                        sourceBody[binding.Target].Parameters.Length <= binding.ParameterPosition ||
                        !ReferenceEquals(
                            sourceBody[binding.Target].Parameters[binding.ParameterPosition],
                            binding.Parameter))
                        throw PointerProofError(
                            participation,
                            function,
                            binding.Source,
                            $"uniform incoming binding arm {binding.Arm}, parameter " +
                            $"{binding.ParameterPosition} changed");
                }
            }
    }

    internal static void CheckTargetCorrespondence(
        ShaderModuleDeclaration<RegionFunctionBody> pointer,
        ShaderModuleDeclaration<SlangFunctionBody> target,
        CLSLCooperationFacts facts) =>
        CooperationTargetVerifier.Verify(pointer, target, facts);

    private static Instruction<IShaderValue, IShaderValue> InstructionAt(
        RegionFunctionBody body,
        Label label,
        int ordinal) =>
        body[label].Body.Elements.ElementAt(ordinal);

    private static bool SameInstruction(
        Instruction<IShaderValue, IShaderValue> before,
        Instruction<IShaderValue, IShaderValue> after) =>
        ReferenceEquals(before.Operation, after.Operation) &&
        ReferenceEquals(before.Result, after.Result) &&
        ReferenceEquals(before.Payload, after.Payload) &&
        before.Operands.SequenceEqual(after.Operands, ReferenceEqualityComparer.Instance);

    private static bool MatchesDependencyFact(
        Instruction<IShaderValue, IShaderValue> instruction,
        CooperationDependencyValueFact fact) =>
        ReferenceEquals(instruction.Operation, fact.Operation) &&
        ReferenceEquals(instruction.Result, fact.Value) &&
        ReferenceEquals(instruction.Payload, fact.Payload) &&
        instruction.Operands.SequenceEqual(fact.Operands, ReferenceEqualityComparer.Instance) &&
        (fact.Callee is null ||
         instruction.OperandCount > 0 && ReferenceEquals(instruction[0], fact.Callee));

    private static bool MatchesInstructionFact(
        Instruction<IShaderValue, IShaderValue> instruction,
        CooperationInstructionFact fact) =>
        ReferenceEquals(instruction.Operation, fact.Operation) &&
        ReferenceEquals(instruction.Result, fact.Result) &&
        ReferenceEquals(instruction.Payload, fact.Payload) &&
        instruction.Operands.SequenceEqual(fact.Operands, ReferenceEqualityComparer.Instance);

    private static RegionJump<IShaderValue> JumpAt(
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
        int arm) =>
        terminator switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch when arm == 0 => branch.Target,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch when arm == 0 => branch.TrueTarget,
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch when arm == 1 => branch.FalseTarget,
            _ => throw new NotSupportedException($"PortableWgsl source control arm {arm} changed.")
        };

    private static NotSupportedException PointerProofError(
        EntryUniformQuadParticipation participation,
        FunctionDeclaration function,
        Label label,
        string reason) =>
        new(
            $"PortableWgsl pointer lowering changed analyzed uniformity proof for entry " +
            $"'{participation.Entry.Name}', function '{function.Name}', block '{label.Name}': {reason}.");

    private static NotSupportedException ShapeError(
        FunctionDeclaration entry,
        FunctionDeclaration function,
        Label label,
        string reason) =>
        new(
            $"PortableWgsl entry '{entry.Name}' requires entry-uniform quad participation; " +
            $"function '{function.Name}', block '{label.Name}': {reason}.");

    private static NotSupportedException Error(
        CLSLCooperationProfile profile,
        string stage,
        OperationRequirementSite site,
        OperationRequirement requirement,
        string reason) =>
        new(
            $"{profile} admission rejected function '{site.Function.Name}', block '{site.Label.Name}', " +
            $"operation '{site.Operation.Name}', requirement '{requirement}', stage " +
            $"'{stage}': {reason}{Provenance(site.Payload)}.");

    private static string Provenance(object? payload) =>
        payload is ShaderStackProvenance provenance
            ? $" at IL_{provenance.ByteStart:X4}..IL_{provenance.ByteEnd:X4}"
            : string.Empty;
}
