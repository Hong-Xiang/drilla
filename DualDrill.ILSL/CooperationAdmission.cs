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

public sealed record EntryUniformQuadParticipation(
    FunctionDeclaration Entry,
    ImmutableDictionary<FunctionDeclaration, ImmutableArray<Label>> OriginalBlocks,
    ImmutableArray<OperationRequirementSite> OriginalSensitiveSites,
    ImmutableArray<CooperationCallSite> CallInheritance);

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
        CLSLCooperationProfile profile)
    {
        var effects = FunctionEffectAnalysis.Analyze(normalized);
        return profile switch
        {
            CLSLCooperationProfile.Scalar => PrepareScalar(effects),
            CLSLCooperationProfile.PortableWgsl => PreparePortable(normalized, effects),
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
        FunctionEffectAnalysisResult effects)
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

        var derivativeTargets = effects.Summaries
            .SelectMany(item => item.Value.RequirementSites.Where(site =>
                ReferenceEquals(site.Function, item.Key) &&
                (site.Requirements & OperationRequirement.DerivativeQuad) != 0))
            .Select(site => (
                Site: site,
                Target: CheckDerivative(
                    normalized,
                    site,
                    StageDescription(owners[site.Function]))))
            .ToImmutableArray();
        foreach (var (site, mappedTarget) in derivativeTargets)
        {
            var collision = normalized.Declarations
                .OfType<FunctionDeclaration>()
                .FirstOrDefault(function =>
                    string.Equals(function.Name, mappedTarget.Name, StringComparison.Ordinal));
            if (collision is not null)
                throw Error(
                    CLSLCooperationProfile.PortableWgsl,
                    StageDescription(owners[site.Function]),
                    site,
                    OperationRequirement.DerivativeQuad,
                    $"mapped target spelling '{mappedTarget.Name}' collides with module declaration " +
                    $"'{collision.Name}'");
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
            var blocks = ImmutableDictionary.CreateBuilder<FunctionDeclaration, ImmutableArray<Label>>();
            foreach (var function in closure)
            {
                foreach (var call in calls[function])
                    CheckOrdinaryCall(normalized, entry.Function, call);
                blocks.Add(function, CheckStraightLine(normalized.FunctionDefinitions[function], entry.Function));
            }

            participation.Add(new EntryUniformQuadParticipation(
                entry.Function,
                blocks.ToImmutable(),
                summary.RequirementSites
                    .Where(static site => (site.Requirements & SensitiveRequirements) != 0)
                    .ToImmutableArray(),
                closure.SelectMany(function => calls[function]).ToImmutableArray()));
        }

        var facts = new CLSLCooperationFacts(participation.ToImmutable());
        if (facts.EntryUniformQuadParticipations.IsEmpty)
            return new CooperationPreparation(facts, null);
        var pointer = normalized.RunPass(new StablePointerRegionParameterPass());
        CheckPointerCorrespondence(normalized, pointer, facts);
        var target = new SlangTargetLowering().Lower(pointer);
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

    private static ImmutableArray<FunctionDeclaration> Closure(
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

    private static ImmutableArray<CooperationCallSite> DirectCalls(
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

    private static ImmutableArray<Label> CheckStraightLine(
        RegionFunctionBody body,
        FunctionDeclaration entry)
    {
        var regions = new Dictionary<Label, RegionTree<Label, ShaderRegionBody>>();
        body.Body.Traverse(region =>
        {
            if (region.Definition.Kind is RegionKind.Loop)
                throw ShapeError(entry, body.Declaration, region.Label, "RegionKind.Loop is not admitted");
            regions.Add(region.Label, region);
        });

        var visited = new HashSet<Label>();
        var ordered = ImmutableArray.CreateBuilder<Label>();
        var current = body.Entry;
        while (true)
        {
            if (!visited.Add(current))
                throw ShapeError(entry, body.Declaration, current, "control revisits a block");
            ordered.Add(current);
            var terminator = body[current].Body.Last;
            switch (terminator)
            {
                case Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>:
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>:
                    if (visited.Count != regions.Count)
                        throw ShapeError(
                            entry,
                            body.Declaration,
                            current,
                            "return leaves original blocks unvisited");
                    return ordered.ToImmutable();
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch:
                    var transfer = body.Control.Resolve(current, 0);
                    if (transfer.Kind is not ScopedContinuationKind.Forward ||
                        !ReferenceEquals(transfer.Target, branch.Target.Label))
                        throw ShapeError(
                            entry,
                            body.Declaration,
                            current,
                            "unconditional edge is not the checked Forward transfer");
                    current = transfer.Target;
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>:
                    throw ShapeError(entry, body.Declaration, current, "conditional control is not admitted");
                case Terminator.D.Switch<RegionJump<IShaderValue>, IShaderValue>:
                    throw ShapeError(entry, body.Declaration, current, "switch control is not admitted");
                default:
                    throw ShapeError(entry, body.Declaration, current, "unsupported terminator");
            }
        }
    }

    private static FunctionDeclaration CheckDerivative(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        OperationRequirementSite site,
        string stage)
    {
        var instruction = InstructionAt(module.FunctionDefinitions[site.Function], site.Label, site.InstructionOrdinal);
        if (instruction.Operation is not CallOperation ||
            instruction.OperandCount == 0 ||
            instruction[0] is not FunctionDeclaration declaration ||
            !PortableDerivativeTarget.TryLower(declaration, out var target))
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
        return target;
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
                var pointerBody = pointer.FunctionDefinitions[function];
                if (!labels.SequenceEqual(pointerBody.Labels))
                    throw new NotSupportedException(
                        $"PortableWgsl pointer lowering changed original block identity/order in " +
                        $"function '{function.Name}' for entry '{participation.Entry.Name}'.");
                foreach (var site in RelevantSites(participation, function))
                {
                    var before = InstructionAt(
                        source.FunctionDefinitions[function],
                        site.Label,
                        site.InstructionOrdinal);
                    var after = InstructionAt(pointerBody, site.Label, site.InstructionOrdinal);
                    if (!SameInstruction(before, after))
                        throw new NotSupportedException(
                            $"PortableWgsl pointer lowering did not preserve exactly one placement of " +
                            $"function '{function.Name}', block '{site.Label.Name}', operation " +
                            $"'{before.Operation.Name}'{Provenance(before.Payload)}.");
                }
            }
    }

    private static void CheckTargetCorrespondence(
        ShaderModuleDeclaration<RegionFunctionBody> pointer,
        ShaderModuleDeclaration<SlangFunctionBody> target,
        CLSLCooperationFacts facts)
    {
        foreach (var participation in facts.EntryUniformQuadParticipations)
            foreach (var (function, labels) in participation.OriginalBlocks)
            {
                var targetBody = target.FunctionDefinitions[function];
                var statements = Statements(targetBody.Body).ToImmutableArray();
                if (statements.Any(static statement =>
                        statement is SlangIf or SlangLoop or SlangContinue))
                    throw new NotSupportedException(
                        $"PortableWgsl target correspondence for entry '{participation.Entry.Name}', " +
                        $"function '{function.Name}' produced conditional/loop/continue target control.");
                foreach (var label in labels)
                    if (statements.Count(statement =>
                            statement is SlangScope { OriginalLabel: var found } &&
                            ReferenceEquals(found, label)) != 1)
                        throw new NotSupportedException(
                            $"PortableWgsl target correspondence for entry '{participation.Entry.Name}', " +
                            $"function '{function.Name}' did not preserve label '{label.Name}' exactly once.");

                var targetInstructions = statements.Select(static statement =>
                        (Instruction<SlangOperand, IShaderValue>?)(statement switch
                        {
                            SlangBind binding => binding.Instruction,
                            SlangEffect effect => effect.Instruction,
                            _ => null
                        }))
                    .OfType<Instruction<SlangOperand, IShaderValue>>()
                    .ToImmutableArray();
                foreach (var site in RelevantSites(participation, function))
                {
                    var sourceInstruction = InstructionAt(
                        pointer.FunctionDefinitions[function],
                        site.Label,
                        site.InstructionOrdinal);
                    if (targetInstructions.Count(instruction =>
                            ReferenceEquals(instruction.Operation, sourceInstruction.Operation) &&
                            ReferenceEquals(instruction.Result, sourceInstruction.Result) &&
                            ReferenceEquals(instruction.Payload, sourceInstruction.Payload)) != 1)
                        throw new NotSupportedException(
                            $"PortableWgsl target correspondence did not preserve exactly one placement of " +
                            $"function '{function.Name}', block '{site.Label.Name}', operation " +
                            $"'{sourceInstruction.Operation.Name}'{Provenance(sourceInstruction.Payload)}.");
                }
            }
    }

    private static IEnumerable<(Label Label, int InstructionOrdinal)> RelevantSites(
        EntryUniformQuadParticipation participation,
        FunctionDeclaration function) =>
        participation.OriginalSensitiveSites
            .Where(site => ReferenceEquals(site.Function, function))
            .Select(static site => (site.Label, site.InstructionOrdinal))
            .Concat(participation.CallInheritance
                .Where(site => ReferenceEquals(site.Caller, function))
                .Select(static site => (site.Label, site.InstructionOrdinal)))
            .Distinct();

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
        ReferenceEquals(before.Payload, after.Payload);

    private static IEnumerable<SlangStatement> Statements(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            switch (statement)
            {
                case SlangScope scope:
                    foreach (var nested in Statements(scope.Body)) yield return nested;
                    break;
                case SlangIf conditional:
                    foreach (var nested in Statements(conditional.WhenTrue)) yield return nested;
                    foreach (var nested in Statements(conditional.WhenFalse)) yield return nested;
                    break;
                case SlangDoOnce once:
                    foreach (var nested in Statements(once.Body)) yield return nested;
                    break;
                case SlangLoop loop:
                    foreach (var nested in Statements(loop.Body)) yield return nested;
                    break;
            }
        }
    }

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
