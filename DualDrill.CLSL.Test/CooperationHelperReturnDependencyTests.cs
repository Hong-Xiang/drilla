using System.Collections.Immutable;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
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
using DualDrill.Mathematics;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CooperationHelperReturnDependencyTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(HelperCase.Identity)]
    [InlineData(HelperCase.FirstArgument)]
    [InlineData(HelperCase.TwoHop)]
    public void PureHelperReturnDependenciesCompileThroughPublicTargets(HelperCase helperCase)
    {
        var shader = Shader(helperCase);
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var cil = raw.FunctionDefinitions.Values.SelectMany(static body => body.Code.Instructions).ToArray();
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var participation = Assert.Single(
            CLSLCooperationAnalysis.Analyze(source).EntryUniformQuadParticipations);
        var helpers = participation.Uniformity.Values
            .Where(facts => !ReferenceEquals(facts.Function, participation.Entry))
            .ToArray();

        Assert.Contains(cil, instruction =>
            instruction.Instruction.OpCode.Name?.StartsWith("call", StringComparison.Ordinal) is true);
        Assert.Contains(cil, instruction =>
            instruction.Instruction.OpCode.Name?.StartsWith("brtrue", StringComparison.Ordinal) is true ||
            instruction.Instruction.OpCode.Name?.StartsWith("brfalse", StringComparison.Ordinal) is true);
        Assert.All(helpers, helper =>
        {
            var dependencies = Assert.IsType<CooperationUniformDependencies.Known>(
                helper.AggregateReturnDependencies);
            Assert.Equal([0], dependencies.FormalParameterPositions.ToArray());
        });
        var entry = participation.Uniformity[participation.Entry];
        Assert.Single(entry.UniformConditionals);
        Assert.Contains(
            entry.DependencyValues,
            fact => fact.Kind is CooperationDependencyValueKind.HelperCallResult &&
                    Assert.IsType<CooperationUniformDependencies.Known>(
                        fact.Dependencies).FormalParameterPositions.IsEmpty);

        var slang = Emit(shader, CLSLCompileTarget.SLang);
        var wgsl = Emit(shader, CLSLCompileTarget.WGSL);
        output.WriteLine($"=== {helperCase} CIL ===");
        foreach (var body in raw.FunctionDefinitions.Values)
            output.WriteLine(body.PrettyPrint());
        output.WriteLine($"=== {helperCase} IR ===");
        output.WriteLine(Emit(shader, CLSLCompileTarget.IR));
        output.WriteLine($"=== {helperCase} Slang ===");
        output.WriteLine(slang);
        output.WriteLine($"=== {helperCase} WGSL ===");
        output.WriteLine(wgsl);
        Assert.Contains("if", slang);
        Assert.Contains("ddx(", slang);
        Assert.Contains("dpdx(", wgsl);
    }

    [Theory]
    [InlineData(typeof(VaryingIdentityShader), "conditional control")]
    [InlineData(typeof(ParameterStoreShader), "conditional control")]
    [InlineData(typeof(ParameterControlShader), "conditional control")]
    [InlineData(typeof(AllReturnDependenciesShader), "conditional control")]
    [InlineData(typeof(ResourceIdentityShader), "conditional control")]
    public void UnprovedHelperDependenciesRemainRejected(Type shaderType, string expected)
    {
        var shader = (ISharpShader)(Activator.CreateInstance(shaderType)
            ?? throw new InvalidOperationException($"Could not create {shaderType}."));

        var error = Assert.Throws<NotSupportedException>(() =>
            Emit(shader, CLSLCompileTarget.IR));

        Assert.Contains(expected, error.Message);
        Assert.Contains("PortableWgsl", error.Message);
    }

    [Fact]
    public void TransferredFormalPointerCannotProveHelperReturn()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            CLSLCooperationAnalysis.Analyze(EscapedFormalModule()));

        Assert.Contains("conditional control is varying", error.Message);
    }

    [Fact]
    public void ParameterDependentNumericBuiltinRetainsTargetCollisionGuard()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            Emit(new ParameterizedBuiltinCollisionShader(), CLSLCompileTarget.IR));

        Assert.Contains("numeric builtin 'sin'", error.Message);
        Assert.Contains("target spelling 'sin'", error.Message);
    }

    [Fact]
    public void DependencyLatticeLawsAndUsedFormalSubstitutionAreExact()
    {
        var unknown = CooperationUniformity.DependencyLattice.Unknown;
        var empty = CooperationUniformity.DependencyLattice.Empty;
        var zero = CooperationUniformity.DependencyLattice.ForFormal(0);
        var one = CooperationUniformity.DependencyLattice.ForFormal(1);
        var zeroOne = AssertKnown(CooperationUniformity.DependencyLattice.Union([one, zero, zero]));
        var canonical = new CooperationUniformDependencies.Known([2, 0, 2]);

        Assert.Equal([0, 1], zeroOne.FormalParameterPositions.ToArray());
        Assert.Equal([0, 2], canonical.FormalParameterPositions.ToArray());
        AssertSame(zero, CooperationUniformity.DependencyLattice.Union([zero, zero]));
        AssertSame(
            CooperationUniformity.DependencyLattice.Union([zero, one]),
            CooperationUniformity.DependencyLattice.Union([one, zero]));
        AssertSame(
            CooperationUniformity.DependencyLattice.Union(
                [CooperationUniformity.DependencyLattice.Union([zero, one]), empty]),
            CooperationUniformity.DependencyLattice.Union(
                [zero, CooperationUniformity.DependencyLattice.Union([one, empty])]));
        Assert.IsType<CooperationUniformDependencies.Unknown>(
            CooperationUniformity.DependencyLattice.Union([zero, unknown]));

        var actuals = new CooperationUniformDependencies[]
        {
            empty,
            unknown
        };
        AssertSame(
            empty,
            CooperationUniformity.DependencyLattice.Substitute(
                zero,
                position => actuals[position]));
        Assert.IsType<CooperationUniformDependencies.Unknown>(
            CooperationUniformity.DependencyLattice.Substitute(
                one,
                position => actuals[position]));
        Assert.IsType<CooperationUniformDependencies.Unknown>(
            CooperationUniformity.DependencyLattice.Substitute(
                unknown,
                position => actuals[position]));
    }

    [Fact]
    public void TargetVerifierRejectsForgedEmptyHelperSummary()
    {
        var prepared = Prepare(new IdentityShader());
        var participation = Assert.Single(prepared.Facts.EntryUniformQuadParticipations);
        var helper = Assert.Single(
            participation.Uniformity.Keys,
            function => function.Name == nameof(IdentityShader.Identity));
        var forgedUniformity = participation.Uniformity.SetItem(
            helper,
            participation.Uniformity[helper] with
            {
                AggregateReturnDependencies = CooperationUniformity.DependencyLattice.Empty
            });
        var forged = new CLSLCooperationFacts(
        [
            participation with { Uniformity = forgedUniformity }
        ]);

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                prepared.Target,
                forged));

        Assert.Contains("dependency proof", error.Message);
    }

    [Fact]
    public void TargetVerifierRejectsFormalLoadReboundToSameTypedParameter()
    {
        var prepared = Prepare(new FirstArgumentShader());
        var helper = prepared.Source.FunctionDefinitions.Keys.Single(
            function => function.Name == nameof(FirstArgumentShader.First));
        var helperBody = prepared.Target.GetBody(helper);
        var load = Assert.Single(
            helperBody.Origins.Definitions,
            origin => origin.Source.Operation is LoadOperation);
        var replacementParameter = helper.Parameters[1];
        var changed = new SlangBind(load.Definition.Instruction with
        {
            Operand0 = new SlangPlaceOperand(new SlangParameterPlace(replacementParameter))
        });
        var corruptedBody = Rewrite(
            helperBody,
            statement => ReferenceEquals(statement, load.Definition) ? changed : statement);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(helper, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("does not preserve its operation/result/operand lineage", error.Message);
    }

    [Fact]
    public void TargetVerifierRejectsChangedHelperActual()
    {
        var prepared = Prepare(new FirstArgumentShader());
        var entryBody = prepared.Target.GetBody(prepared.Entry);
        var call = Assert.Single(
            entryBody.Origins.Definitions,
            origin => origin.Source.Operation is CallOperation &&
                      origin.Source.Operand0 is FunctionDeclaration function &&
                      function.Name == nameof(FirstArgumentShader.First));
        var operands = call.Definition.Instruction.Operands.ToImmutableArray();
        var changed = new SlangBind(call.Definition.Instruction with
        {
            RestOperands = [operands[2], operands[1]]
        });
        var corruptedBody = Rewrite(
            entryBody,
            statement => ReferenceEquals(statement, call.Definition) ? changed : statement);
        var corrupted = new ShaderModuleDeclaration<SlangFunctionBody>(
            prepared.Target.Declarations,
            prepared.Target.FunctionDefinitions.SetItem(prepared.Entry, corruptedBody));

        var error = Assert.Throws<NotSupportedException>(() =>
            CooperationAdmission.CheckTargetCorrespondence(
                prepared.Pointer,
                corrupted,
                prepared.Facts));

        Assert.Contains("target correspondence", error.Message);
    }

    private static ISharpShader Shader(HelperCase helperCase) =>
        helperCase switch
        {
            HelperCase.Identity => new IdentityShader(),
            HelperCase.FirstArgument => new FirstArgumentShader(),
            HelperCase.TwoHop => new TwoHopShader(),
            _ => throw new ArgumentOutOfRangeException(nameof(helperCase), helperCase, null)
        };

    private static string Emit(ISharpShader shader, CLSLCompileTarget target) =>
        new CLSLCompiler(new(target, CLSLCooperationProfile.PortableWgsl)).Emit(shader);

    private static Prepared Prepare(ISharpShader shader)
    {
        var raw = new RuntimeReflectionParser(CompilationContext.Create()).ParseShaderModule(shader);
        var source = CilModuleCompiler.Compile(raw).RunPass(new FunctionToOperationPass());
        var facts = CLSLCooperationAnalysis.Analyze(source);
        var pointer = source.RunPass(new StablePointerRegionParameterPass());
        var target = new SlangTargetLowering().Lower(pointer);
        var entry = source.FunctionDefinitions.Keys.Single(function =>
            function.Attributes.Any(attribute => attribute is FragmentAttribute));
        return new(source, pointer, target, facts, entry);
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> EscapedFormalModule()
    {
        var helperParameter = new ParameterDeclaration("value", ShaderType.Bool, []);
        var helper = new FunctionDeclaration(
            "Escaped",
            [helperParameter],
            new FunctionReturn(ShaderType.Bool, []),
            []);
        var helperEntry = Label.Create("helper-entry");
        var helperReturn = Label.Create("helper-return");
        var pointerParameter = ShaderValue.Intermediate(helperParameter.Value.Type);
        var loaded = ShaderValue.Intermediate(ShaderType.Bool);
        var helperBody = RegionFixture.CreateFunctionBody(
            helper,
            RegionTree.Block(
                helperEntry,
                [
                    RegionTree.Block(
                        helperReturn,
                        [],
                        RegionFixture.Body(
                            helperReturn,
                            [pointerParameter],
                            [Instruction.Factory.Load(
                                default,
                                new LoadOperation(),
                                loaded,
                                pointerParameter)],
                            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(loaded)),
                        null)
                ],
                RegionFixture.Body(
                    helperEntry,
                    [],
                    [],
                    Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(
                        new(helperReturn, [helperParameter.Value]))),
                helperReturn));

        var entry = new FunctionDeclaration(
            "Fragment",
            [],
            new FunctionReturn(ShaderType.F32, [new LocationAttribute(0)]),
            [new FragmentAttribute()]);
        var entryLabel = Label.Create("entry");
        var derivativeLabel = Label.Create("derivative");
        var fallbackLabel = Label.Create("fallback");
        var condition = ShaderValue.Intermediate(ShaderType.Bool);
        var derivative = ShaderValue.Intermediate(ShaderType.F32);
        var dpdx = ShaderFunction.Instance.GetFunction("dpdx", ShaderType.F32, ShaderType.F32);
        var entryBody = RegionFixture.Body(
            entryLabel,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)helper.Type),
                    condition,
                    [helper, ShaderValue.Literal(new BoolLiteral(true))])
            ],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition,
                new(derivativeLabel, []),
                new(fallbackLabel, [])));
        var derivativeBody = RegionFixture.Body(
            derivativeLabel,
            [],
            [
                Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)dpdx.Type),
                    derivative,
                    [dpdx, ShaderValue.Literal(new F32Literal(1.0f))])
            ],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(derivative));
        var fallbackBody = RegionFixture.Body(
            fallbackLabel,
            [],
            [],
            Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(
                ShaderValue.Literal(new F32Literal(0.0f))));
        var entryFunctionBody = RegionFixture.CreateFunctionBody(
            entry,
            RegionTree.Block(
                entryLabel,
                [
                    RegionTree.Block(fallbackLabel, [], fallbackBody, null),
                    RegionTree.Block(derivativeLabel, [], derivativeBody, null)
                ],
                entryBody,
                derivativeLabel));

        return new(
            [entry, helper],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty
                .Add(entry, entryFunctionBody)
                .Add(helper, helperBody));
    }

    private static CooperationUniformDependencies.Known AssertKnown(
        CooperationUniformDependencies dependencies) =>
        Assert.IsType<CooperationUniformDependencies.Known>(dependencies);

    private static void AssertSame(
        CooperationUniformDependencies expected,
        CooperationUniformDependencies actual) =>
        Assert.True(CooperationUniformity.DependencyLattice.Equals(expected, actual));

    private static SlangFunctionBody Rewrite(
        SlangFunctionBody source,
        Func<SlangStatement, SlangStatement> rewrite)
    {
        var replacements = new Dictionary<SlangStatement, SlangStatement>(
            ReferenceEqualityComparer.Instance);

        SlangBlock Visit(SlangBlock block)
        {
            var statements = block.Statements.Select(original =>
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
                var changed = ReferenceEquals(requested, original) ? nested : requested;
                replacements.Add(original, changed);
                return changed;
            });
            return new([.. statements]);
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
            Instructions = [.. source.Origins.Instructions.Select(origin => origin with
            {
                Target = Replace(origin.Target)
            })],
            Returns = [.. source.Origins.Returns.Select(origin => origin with
            {
                Return = Replace(origin.Return)
            })]
        };
        return new(source.Declaration, body, origins);
    }

    public enum HelperCase
    {
        Identity,
        FirstArgument,
        TwoHop
    }

    private sealed record Prepared(
        ShaderModuleDeclaration<RegionFunctionBody> Source,
        ShaderModuleDeclaration<RegionFunctionBody> Pointer,
        ShaderModuleDeclaration<SlangFunctionBody> Target,
        CLSLCooperationFacts Facts,
        FunctionDeclaration Entry);

    private sealed class IdentityShader : ISharpShader
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
        public static bool Identity(bool value) => value;
    }

    private sealed class FirstArgumentShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (First(true, varying > 0.0f))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        public static bool First(bool value, bool _) => value;
    }

    private sealed class TwoHopShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Outer(true))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        public static bool Outer(bool value) => Identity(value);

        [ShaderMethod]
        public static bool Identity(bool value) => value;
    }

    private sealed class VaryingIdentityShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Identity(varying > 0.0f))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static bool Identity(bool value) => value;
    }

    private sealed class ParameterStoreShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Replace(true, varying > 0.0f))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static bool Replace(bool value, bool replacement)
        {
            value = replacement;
            return value;
        }
    }

    private sealed class ParameterControlShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Choose(true))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static bool Choose(bool value)
        {
            if (value)
                return True();
            return False();
        }

        [ShaderMethod]
        private static bool True() => true;

        [ShaderMethod]
        private static bool False() => false;
    }

    private sealed class ResourceIdentityShader : ISharpShader
    {
#pragma warning disable CS0649
        [Group(0), Binding(0)]
        private static StructuredBuffer<float> Input;
#pragma warning restore CS0649

        [Fragment]
        [return: Location(0)]
        public static float Fragment()
        {
            if (Identity(Input.Length > 0u))
                return DMath.dpdx(0.0f);
            return 0.0f;
        }

        [ShaderMethod]
        private static bool Identity(bool value) => value;
    }

    private sealed class AllReturnDependenciesShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Choose(true, varying > 0.0f))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static bool Choose(bool first, bool second)
        {
            if (UniformChoice())
                return first;
            return second;
        }

        [ShaderMethod]
        private static bool UniformChoice() => true;
    }

    private sealed class ParameterizedBuiltinCollisionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static float Fragment([Location(0)] float varying)
        {
            if (Positive(0.0f))
                return DMath.dpdx(varying);
            return varying;
        }

        [ShaderMethod]
        private static bool Positive(float value) => DMath.sin(value) >= 0.0f;

        [ShaderMethod]
        private static float sin(float value) => value;
    }
}
