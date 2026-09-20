using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
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
using DualDrill.Common.Nat;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.Test;

public sealed class FunctionEffectAnalysisTests
{
    public static TheoryData<string> DerivativeNames => new()
    {
        nameof(Dpdx),
        nameof(DpdxCoarse),
        nameof(DpdxFine),
        nameof(Dpdy),
        nameof(DpdyCoarse),
        nameof(DpdyFine),
        nameof(Fwidth),
        nameof(FwidthCoarse),
        nameof(FwidthFine)
    };

    [Fact]
    public void OperationRequirementHasStableSharedFlagValues()
    {
        Assert.Equal(0, (int)OperationRequirement.None);
        Assert.Equal(1, (int)OperationRequirement.MemoryRead);
        Assert.Equal(2, (int)OperationRequirement.MemoryWrite);
        Assert.Equal(4, (int)OperationRequirement.DerivativeQuad);
        Assert.Equal(8, (int)OperationRequirement.SubgroupParticipation);
        Assert.Equal(16, (int)OperationRequirement.WorkgroupBarrier);
    }

    [Theory]
    [MemberData(nameof(DerivativeNames))]
    public void ActualCilRecognizesEveryDerivativeBuiltin(string methodName)
    {
        var method = Method(methodName);
        var module = Normalize(CompilerTestPipeline.CompileStages(method).Compiled);
        var function = Function(module, methodName);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.True(summary.IsComplete);
        Assert.Equal(
            OperationRequirement.MemoryRead | OperationRequirement.DerivativeQuad,
            summary.Requirements);
        var site = Assert.Single(
            summary.RequirementSites,
            site => site.Requirements == OperationRequirement.DerivativeQuad);
        Assert.Same(function, site.Function);
        Assert.Equal(OperationRequirement.DerivativeQuad, site.Requirements);
        Assert.Same(
            Instructions(module.FunctionDefinitions[function])
                .Single(instruction => instruction.Operation is CallOperation)
                .Payload,
            site.Payload);
    }

    [Fact]
    public void ActualCilPropagatesDerivativeOriginsThroughHelpers()
    {
        var module = Normalize(CompilerTestPipeline.CompileStages(Method(nameof(TwoHopDerivative))).Compiled);
        var direct = Function(module, nameof(DirectDerivative));
        var oneHop = Function(module, nameof(OneHopDerivative));
        var twoHop = Function(module, nameof(TwoHopDerivative));

        var result = FunctionEffectAnalysis.Analyze(module);

        foreach (var function in new[] { direct, oneHop, twoHop })
        {
            var summary = result[function];
            Assert.True(summary.IsComplete);
            Assert.Equal(
                OperationRequirement.MemoryRead | OperationRequirement.DerivativeQuad,
                summary.Requirements);
            Assert.Same(
                direct,
                Assert.Single(
                    summary.RequirementSites,
                    site => site.Requirements == OperationRequirement.DerivativeQuad).Function);
        }
    }

    [Fact]
    public void ActualCilScalarAndSameNamedUserHelperRemainPure()
    {
        var scalarModule = Normalize(CompilerTestPipeline.CompileStages(Method(nameof(Scalar))).Compiled);
        var fakeModule = Normalize(CompilerTestPipeline.CompileStages(Method(nameof(CallUserDpdx))).Compiled);

        var scalar = FunctionEffectAnalysis.Analyze(scalarModule)[Function(scalarModule, nameof(Scalar))];
        var fake = FunctionEffectAnalysis.Analyze(fakeModule)[Function(fakeModule, nameof(CallUserDpdx))];

        Assert.True(scalar.IsComplete);
        Assert.Equal(OperationRequirement.MemoryRead, scalar.Requirements);
        Assert.True(fake.IsComplete);
        Assert.Equal(OperationRequirement.MemoryRead, fake.Requirements);
        Assert.DoesNotContain(
            fake.RequirementSites,
            site => site.Requirements == OperationRequirement.DerivativeQuad);
    }

    [Fact]
    public void ClassifiesMemoryAndVectorPointerOperations()
    {
        var vec = VecType<N2, FloatType<N32>>.Instance;
        var ptr = ShaderValue.Intermediate(vec.GetPtrType());
        var scalar = ShaderValue.Intermediate(ShaderType.F32);
        var vector = ShaderValue.Intermediate(vec);
        var instructions = new[]
        {
            Instruction<IShaderValue, IShaderValue>.Create(
                new LoadOperation(), scalar, [ShaderValue.Intermediate(ShaderType.F32.GetPtrType())]),
            Instruction<IShaderValue, IShaderValue>.Create(
                new StoreOperation(), null, [ShaderValue.Intermediate(ShaderType.F32.GetPtrType()), scalar]),
            Instruction<IShaderValue, IShaderValue>.Create(
                VectorComponentGetExpressionOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.X>.Instance,
                scalar,
                [ptr]),
            Instruction<IShaderValue, IShaderValue>.Create(
                VectorComponentSetOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.X>.Instance,
                null,
                [ptr, scalar]),
            Instruction<IShaderValue, IShaderValue>.Create(
                VectorSwizzleGetExpressionOperation<Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y>, FloatType<N32>>.Instance,
                vector,
                [ptr]),
            Instruction<IShaderValue, IShaderValue>.Create(
                VectorSwizzleSetOperation<Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y>, FloatType<N32>>.Instance,
                null,
                [ptr, vector])
        };
        var (module, function) = Module("Memory", instructions);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.True(summary.IsComplete);
        Assert.Equal(
            OperationRequirement.MemoryRead | OperationRequirement.MemoryWrite,
            summary.Requirements);
        Assert.Equal(3, summary.RequirementSites.Count(site =>
            site.Requirements == OperationRequirement.MemoryRead));
        Assert.Equal(3, summary.RequirementSites.Count(site =>
            site.Requirements == OperationRequirement.MemoryWrite));
    }

    [Fact]
    public void ProviderNoneMixedAndUnsupportedBitsAreExplicit()
    {
        const OperationRequirement unsupported = (OperationRequirement)32;
        var instructions = new[]
        {
            Op(new ProviderOperation(OperationRequirement.None)),
            Op(new ProviderOperation(
                OperationRequirement.MemoryRead | OperationRequirement.WorkgroupBarrier)),
            Op(new ProviderOperation(OperationRequirement.MemoryWrite | unsupported))
        };
        var (module, function) = Module("Providers", instructions);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.False(summary.IsComplete);
        Assert.Equal(
            OperationRequirement.MemoryRead |
            OperationRequirement.MemoryWrite |
            OperationRequirement.WorkgroupBarrier,
            summary.Requirements);
        Assert.Equal(2, summary.RequirementSites.Length);
        var unknown = Assert.Single(summary.UnknownSites);
        Assert.Equal(FunctionEffectUnknownReason.UnsupportedRequirement, unknown.Reason);
        Assert.Equal(unsupported, unknown.UnsupportedRequirements);
    }

    [Fact]
    public void UnknownOperationsIncludingExtensibleFamiliesRemainUnknown()
    {
        var instructions = new[]
        {
            Op(new UnknownOperation()),
            Op(new ExtensibleUnaryOperation()),
            Op(new ExtensibleBinaryOperation())
        };
        var (module, function) = Module("UnknownOperations", instructions);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.False(summary.IsComplete);
        Assert.Equal(OperationRequirement.None, summary.Requirements);
        Assert.Equal(
            Enumerable.Repeat(FunctionEffectUnknownReason.UnknownOperation, 3),
            summary.UnknownSites.Select(static site => site.Reason));
    }

    [Fact]
    public void WrappedCustomOperatorTagsRemainUnknown()
    {
        var instructions = new[]
        {
            Op(UnaryNumericArithmeticExpressionOperation<FloatType<N32>, CustomUnaryOperator>.Instance),
            Op(NumericBinaryArithmeticOperation<FloatType<N32>, CustomArithmeticOperator>.Instance),
            Op(NumericBinaryRelationalOperation<FloatType<N32>, CustomRelationalOperator>.Instance),
            Op(LogicalBinaryOperation<CustomLogicalOperator>.Instance),
            Op(VectorNumericUnaryOperation<N2, FloatType<N32>, CustomUnaryOperator>.Instance),
            Op(VectorExpressionNumericBinaryExpressionOperation<
                N2, FloatType<N32>, CustomArithmeticOperator>.Instance),
            Op(ScalarVectorExpressionNumericOperation<
                N2, FloatType<N32>, CustomArithmeticOperator>.Instance),
            Op(VectorScalarExpressionNumericOperation<
                N2, FloatType<N32>, CustomArithmeticOperator>.Instance)
        };
        var (module, function) = Module("WrappedCustomOperators", instructions);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.False(summary.IsComplete);
        Assert.Equal(OperationRequirement.None, summary.Requirements);
        Assert.Equal(8, summary.UnknownSites.Length);
        Assert.All(
            summary.UnknownSites,
            static site => Assert.Equal(FunctionEffectUnknownReason.UnknownOperation, site.Reason));
    }

    [Fact]
    public void KnownWrappedOperatorTagsRemainPure()
    {
        var instructions = new[]
        {
            Op(UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate>.Instance),
            Op(NumericBinaryArithmeticOperation<FloatType<N32>, BinaryArithmetic.Add>.Instance),
            Op(NumericBinaryRelationalOperation<FloatType<N32>, BinaryRelational.Lt>.Instance),
            Op(LogicalBinaryOperation<LogicalAnd>.Instance),
            Op(VectorNumericUnaryOperation<N2, FloatType<N32>, UnaryArithmetic.Negate>.Instance),
            Op(VectorExpressionNumericBinaryExpressionOperation<
                N2, FloatType<N32>, BinaryArithmetic.Add>.Instance),
            Op(ScalarVectorExpressionNumericOperation<
                N2, FloatType<N32>, BinaryArithmetic.Add>.Instance),
            Op(VectorScalarExpressionNumericOperation<
                N2, FloatType<N32>, BinaryArithmetic.Add>.Instance)
        };
        var (module, function) = Module("KnownWrappedOperators", instructions);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.True(summary.IsComplete);
        Assert.Equal(OperationRequirement.None, summary.Requirements);
        Assert.Empty(summary.RequirementSites);
        Assert.Empty(summary.UnknownSites);
    }

    [Fact]
    public void UnknownAndMissingCalleesRemainDistinct()
    {
        var missing = Function("Missing");
        var functionType = (FunctionType)missing.Type;
        var unknownValue = ShaderValue.Intermediate(functionType);
        var instructions = new[]
        {
            Instruction<IShaderValue, IShaderValue>.Create(
                new CallOperation(functionType), null, [unknownValue], "unknown"),
            Call(missing, "missing")
        };
        var (module, function) = Module("Caller", instructions);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.False(summary.IsComplete);
        Assert.Equal(
            new[]
            {
                FunctionEffectUnknownReason.UnknownCallee,
                FunctionEffectUnknownReason.MissingCalleeBody
            },
            summary.UnknownSites.Select(static site => site.Reason));
        Assert.Same(unknownValue, summary.UnknownSites[0].Callee);
        Assert.Same(missing, summary.UnknownSites[1].Callee);
    }

    [Fact]
    public void SameNamedFakeDerivativeDeclarationIsNotBuiltin()
    {
        var builtin = ShaderFunction.Instance.GetFunction(
            nameof(NumericBuiltinFunctionName.dpdx), ShaderType.F32, ShaderType.F32);
        var fake = new FunctionDeclaration(
            builtin.Name,
            builtin.Parameters,
            builtin.Return,
            builtin.Attributes);
        var (module, function) = Module("FakeDerivativeCaller", [Call(fake)]);

        var summary = FunctionEffectAnalysis.Analyze(module)[function];

        Assert.Equal(OperationRequirement.None, summary.Requirements);
        Assert.Equal(
            FunctionEffectUnknownReason.MissingCalleeBody,
            Assert.Single(summary.UnknownSites).Reason);
    }

    [Fact]
    public void SelfAndMutualCyclesKeepKnownEffectsAndMarkDependentsIncomplete()
    {
        var self = Function("Self");
        var selfModule = Module(
            (self, Body(self, [Op(new StoreOperation()), Call(self)])));
        var selfSummary = FunctionEffectAnalysis.Analyze(selfModule)[self];
        Assert.Equal(OperationRequirement.MemoryWrite, selfSummary.Requirements);
        Assert.Contains(
            selfSummary.UnknownSites,
            site => site.Reason == FunctionEffectUnknownReason.RecursiveCallCycle);

        var a = Function("A");
        var b = Function("B");
        var caller = Function("Caller");
        var module = Module(
            (caller, Body(caller, [Call(a)])),
            (b, Body(b, [Op(new StoreOperation(), "write"), Call(a, "b-to-a")])),
            (a, Body(a, [Op(new LoadOperation(), "read"), Call(b, "a-to-b")])));

        var result = FunctionEffectAnalysis.Analyze(module);
        foreach (var function in new[] { a, b, caller })
        {
            var summary = result[function];
            Assert.False(summary.IsComplete);
            Assert.Equal(
                OperationRequirement.MemoryRead | OperationRequirement.MemoryWrite,
                summary.Requirements);
            Assert.Equal(2, summary.RequirementSites.Length);
            Assert.Equal(
                2,
                summary.UnknownSites.Count(site =>
                    site.Reason == FunctionEffectUnknownReason.RecursiveCallCycle));
        }
    }

    [Fact]
    public void DiamondCallsDeduplicateOriginalEffectSiteAndIsolateUnrelatedDefinitions()
    {
        var leaf = Function("Leaf");
        var left = Function("Left");
        var right = Function("Right");
        var root = Function("Root");
        var unrelated = Function("Unrelated");
        var payload = new object();
        var derivative = ShaderFunction.Instance.GetFunction(
            nameof(NumericBuiltinFunctionName.dpdx), ShaderType.F32, ShaderType.F32);
        var module = Module(
            (right, Body(right, [Call(leaf)])),
            (unrelated, Body(unrelated, [Op(new UnknownOperation())])),
            (root, Body(root, [Call(left), Call(right)])),
            (leaf, Body(leaf, [Call(derivative, payload)])),
            (left, Body(left, [Call(leaf)])));

        var result = FunctionEffectAnalysis.Analyze(module);
        var rootSummary = result[root];

        Assert.True(rootSummary.IsComplete);
        Assert.Equal(OperationRequirement.DerivativeQuad, rootSummary.Requirements);
        var site = Assert.Single(rootSummary.RequirementSites);
        Assert.Same(leaf, site.Function);
        Assert.Same(payload, site.Payload);
        Assert.False(result[unrelated].IsComplete);
        Assert.Empty(rootSummary.UnknownSites);
    }

    [Fact]
    public void ResultsIgnoreFunctionStorageOrderAndLabelNames()
    {
        var leaf = Function("Leaf");
        var root = Function("Root");
        var derivative = ShaderFunction.Instance.GetFunction(
            nameof(NumericBuiltinFunctionName.dpdx), ShaderType.F32, ShaderType.F32);
        var firstLeaf = Body(leaf, [Call(derivative, "origin")], "first-leaf");
        var firstRoot = Body(root, [Call(leaf)], "first-root");
        var secondLeaf = Body(leaf, [Call(derivative, "origin")], "renamed-leaf");
        var secondRoot = Body(root, [Call(leaf)], "renamed-root");
        var first = Module((leaf, firstLeaf), (root, firstRoot));
        var second = Module((root, secondRoot), (leaf, secondLeaf));

        var firstSummary = FunctionEffectAnalysis.Analyze(first)[root];
        var secondSummary = FunctionEffectAnalysis.Analyze(second)[root];

        Assert.Equal(firstSummary.Requirements, secondSummary.Requirements);
        Assert.Equal(firstSummary.IsComplete, secondSummary.IsComplete);
        Assert.Equal(
            firstSummary.RequirementSites.Select(SiteShape),
            secondSummary.RequirementSites.Select(SiteShape));
    }

    [Fact]
    public void NormalizationPreservesPayloadForEveryReplacementAndPassesUnknownsThrough()
    {
        var vec = VecType<N2, FloatType<N32>>.Instance;
        var ptr = ShaderValue.Intermediate(vec.GetPtrType());
        var scalar = ShaderValue.Intermediate(ShaderType.F32);
        var vector = ShaderValue.Intermediate(vec);
        var replacements = new (IOperation Operation, IShaderValue? Result, IShaderValue[] Arguments)[]
        {
            (
                NumericBinaryArithmeticOperation<FloatType<N32>, BinaryArithmetic.Add>.Instance,
                scalar,
                [scalar, scalar]),
            (
                VectorComponentSetOperation<N2, VecType<N2, FloatType<N32>>, Swizzle.X>.Instance,
                null,
                [ptr, scalar]),
            (
                VectorSwizzleSetOperation<Swizzle.Pattern<N2, Swizzle.X, Swizzle.Y>, FloatType<N32>>.Instance,
                null,
                [ptr, vector]),
            (
                UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate>.Instance,
                scalar,
                [scalar]),
            (
                new ZeroConstructorOperation(vec),
                vector,
                []),
            (
                VectorCompositeConstructionOperation.Get(vec, [ShaderType.F32, ShaderType.F32]),
                vector,
                [scalar, scalar])
        };
        var calls = replacements.Select((item, index) =>
        {
            var payload = new Payload(index);
            return (
                Instruction: Instruction<IShaderValue, IShaderValue>.Create(
                    new CallOperation((FunctionType)item.Operation.Function.Type),
                    item.Result,
                    [item.Operation.Function, .. item.Arguments],
                    payload),
                Payload: payload);
        }).ToArray();
        var unknown = Op(new UnknownOperation(), new Payload(100));
        var nonDeclarationCallee = Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)Function("Indirect").Type),
            null,
            [ShaderValue.Intermediate(Function("IndirectValue").Type)],
            new Payload(101));
        var declaration = Function("Normalize");
        var original = Body(
            declaration,
            [.. calls.Select(static item => item.Instruction), unknown, nonDeclarationCallee]);

        var normalized = new FunctionToOperationPass().VisitFunctionBody(original);
        var instructions = Instructions(normalized).ToArray();

        for (var index = 0; index < calls.Length; index++)
        {
            Assert.IsNotType<CallOperation>(instructions[index].Operation);
            Assert.Same(calls[index].Payload, instructions[index].Payload);
        }
        Assert.Equal(unknown, instructions[^2]);
        Assert.Equal(nonDeclarationCallee, instructions[^1]);
    }

    private static float Dpdx(float value) => DMath.dpdx(value);
    private static float DpdxCoarse(float value) => DMath.dpdxCoarse(value);
    private static float DpdxFine(float value) => DMath.dpdxFine(value);
    private static float Dpdy(float value) => DMath.dpdy(value);
    private static float DpdyCoarse(float value) => DMath.dpdyCoarse(value);
    private static float DpdyFine(float value) => DMath.dpdyFine(value);
    private static float Fwidth(float value) => DMath.fwidth(value);
    private static float FwidthCoarse(float value) => DMath.fwidthCoarse(value);
    private static float FwidthFine(float value) => DMath.fwidthFine(value);
    private static float DirectDerivative(float value) => DMath.dpdx(value);
    private static float OneHopDerivative(float value) => DirectDerivative(value);
    private static float TwoHopDerivative(float value) => OneHopDerivative(value);
    private static float Scalar(float value) => value + 1f;
    private static float dpdx(float value) => value + 1f;
    private static float CallUserDpdx(float value) => dpdx(value);

    private static MethodInfo Method(string name) =>
        typeof(FunctionEffectAnalysisTests).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new InvalidOperationException(name);

    private static ShaderModuleDeclaration<RegionFunctionBody> Normalize(
        ShaderModuleDeclaration<RegionFunctionBody> module) =>
        module.RunPass(new FunctionToOperationPass());

    private static FunctionDeclaration Function(
        ShaderModuleDeclaration<RegionFunctionBody> module,
        string name) =>
        Assert.Single(module.FunctionDefinitions.Keys, function => function.Name == name);

    private static FunctionDeclaration Function(string name) =>
        new(name, [], new FunctionReturn(UnitType.Instance, []), []);

    private static (
        ShaderModuleDeclaration<RegionFunctionBody> Module,
        FunctionDeclaration Function) Module(
        string name,
        IEnumerable<Instruction<IShaderValue, IShaderValue>> instructions)
    {
        var function = Function(name);
        return (Module((function, Body(function, instructions))), function);
    }

    private static ShaderModuleDeclaration<RegionFunctionBody> Module(
        params (FunctionDeclaration Function, RegionFunctionBody Body)[] definitions) =>
        new(
            [.. definitions.Select(static definition => definition.Function)],
            definitions.ToImmutableDictionary(
                static definition => definition.Function,
                static definition => definition.Body));

    private static RegionFunctionBody Body(
        FunctionDeclaration function,
        IEnumerable<Instruction<IShaderValue, IShaderValue>> instructions,
        string labelName = "entry")
    {
        var label = Label.Create(labelName);
        return RegionFixture.CreateFunctionBody(
            function,
            RegionTree.Block(
                label,
                [],
                RegionFixture.Body(
                    label,
                    [],
                    instructions,
                    Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>()),
                null));
    }

    private static Instruction<IShaderValue, IShaderValue> Call(
        FunctionDeclaration callee,
        object? payload = null) =>
        Instruction<IShaderValue, IShaderValue>.Create(
            new CallOperation((FunctionType)callee.Type),
            null,
            [callee],
            payload);

    private static Instruction<IShaderValue, IShaderValue> Op(
        IOperation operation,
        object? payload = null) =>
        Instruction<IShaderValue, IShaderValue>.Create(operation, null, [], payload);

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

    private static object SiteShape(OperationRequirementSite site) =>
        new
        {
            FunctionName = site.Function.Name,
            site.InstructionOrdinal,
            OperationName = site.Operation.Name,
            site.Requirements,
            site.Payload
        };

    private sealed record Payload(int Value);

    private sealed class ProviderOperation(OperationRequirement requirements)
        : UnknownOperation, IOperationRequirementProvider
    {
        public OperationRequirement Requirements { get; } = requirements;
    }

    private class UnknownOperation : IOperation
    {
        public FunctionDeclaration Function => throw new NotSupportedException();
        public string Name => "unknown";
        public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotSupportedException();

        public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> inst, TS semantic)
            where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
            throw new InvalidOperationException("Unknown operations must not be visited.");
    }

    private sealed class ExtensibleUnaryOperation : IUnaryExpressionOperation
    {
        public IShaderType SourceType => ShaderType.F32;
        public IShaderType ResultType => ShaderType.F32;
        public FunctionDeclaration Function => throw new NotSupportedException();
        public string Name => "extensible-unary";
        public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotSupportedException();

        public TR Evaluate<TX, TR>(IUnaryExpressionOperationSemantic<TX, TR> semantic, TX context) =>
            throw new InvalidOperationException("Unknown operations must not be visited.");
    }

    private sealed class ExtensibleBinaryOperation : IBinaryExpressionOperation
    {
        public IShaderType LeftType => ShaderType.F32;
        public IShaderType RightType => ShaderType.F32;
        public IShaderType ResultType => ShaderType.F32;
        public IBinaryOp BinaryOp => BinaryArithmetic.Add.Instance;
        public FunctionDeclaration Function => throw new NotSupportedException();
        public string Name => "extensible-binary";
        public IOperationMethodAttribute GetOperationMethodAttribute() => throw new NotSupportedException();
    }

    private sealed class CustomUnaryOperator : UnaryArithmetic.IOp<CustomUnaryOperator>
    {
        public static UnaryArithmetic.OpKind Kind => UnaryArithmetic.OpKind.neg;
        public static CustomUnaryOperator Instance { get; } = new();
    }

    private sealed class CustomArithmeticOperator : BinaryArithmetic.IOp<CustomArithmeticOperator>
    {
        public static BinaryArithmetic.OpKind Kind => BinaryArithmetic.OpKind.add;
        public static CustomArithmeticOperator Instance { get; } = new();
    }

    private sealed class CustomRelationalOperator : BinaryRelational.IOp<CustomRelationalOperator>
    {
        public static BinaryRelational.OpKind Kind => BinaryRelational.OpKind.lt;
        public static CustomRelationalOperator Instance { get; } = new();
    }

    private sealed class CustomLogicalOperator : BinaryLogical.IOp<CustomLogicalOperator>
    {
        public static CustomLogicalOperator Instance { get; } = new();
        public string Name => "custom-logical";
    }
}
