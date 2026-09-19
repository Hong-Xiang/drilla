using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Transform;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class CilLocalPromotionPassTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, 36)]
    [InlineData(false, 48)]
    public void OrdinaryCilDiamondUsesPromotedPublicStages(bool choose, int expected) =>
        AssertPublicStages(
            GetMethod(nameof(LocalDiamond)),
            [new Value.Boolean(choose), new Value.Integer(11), new Value.Integer(29)],
            new Value.Integer(expected));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(5, 10)]
    public void OrdinaryCilLoopUsesPromotedPublicStages(int limit, int expected) =>
        AssertPublicStages(
            GetMethod(nameof(LocalLoop)),
            [new Value.Integer(limit)],
            new Value.Integer(expected));

    [Theory]
    [InlineData(EmittedFixtures.InitIntName, 0)]
    [InlineData(EmittedFixtures.InitBoolName, false)]
    public void InitLocalsUsesExactTypedMetadataSeed(string name, object expectedData)
    {
        var method = EmittedFixtures.Method(name);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var lifted = Body(stages.ValueControlFlow, method);
        var promoted = Body(stages.PromotedValueControlFlow, method);

        Assert.True(raw.Code.Environment.Body?.InitLocals is true);
        Assert.NotEmpty(LocalMemory(lifted));
        Assert.Empty(LocalMemory(promoted));
        var expected = expectedData is bool boolean
            ? (Value)new Value.Boolean(boolean)
            : new Value.Integer((int)expectedData);
        AssertEquivalent(RunValueCfg(lifted, []), RunValueCfg(promoted, []));
        Assert.Equal(expected, RunValueCfg(promoted, []).Result);
        Assert.Contains(
            Values(promoted),
            value => value is LiteralValue literal &&
                     (expectedData is bool
                         ? literal.Value is BoolLiteral { Value: false }
                         : literal.Value is I32Literal { Value: 0 }));
    }

    [Fact]
    public void InitLocalsFalseMissingDefinitionRemainsMemory()
    {
        var method = EmittedFixtures.Method(EmittedFixtures.MissingDefinitionName);
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var lifted = Body(stages.ValueControlFlow, method);
        var promoted = Body(stages.PromotedValueControlFlow, method);

        Assert.True(raw.Code.Environment.Body?.InitLocals is false);
        Assert.NotEmpty(LocalMemory(lifted));
        Assert.NotEmpty(LocalMemory(promoted));
        Assert.Same(lifted.Graph, promoted.Graph);
    }

    [Fact]
    public async Task PromotedBoolInitializationEmitsValidSlang()
    {
        var body = CompilerTestPipeline.CompileBody(EmittedFixtures.Method(EmittedFixtures.InitBoolName));
        var lowered = new RegionParameterToLocalVariablePass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(body));
        var source = ScalarControlFlowTests.Emit(lowered);
        output.WriteLine(source);

        await new SlangService().ValidateAsync(source);
    }

    [Fact]
    public void EscapedAndUnsupportedNeighborsRemainMemoryWhileIndependentLocalPromotes()
    {
        var method = EmittedFixtures.Method(EmittedFixtures.StorageBoundariesName);
        var stages = CompilerTestPipeline.CompileStages(method);
        var lifted = Body(stages.ValueControlFlow, method);
        var promoted = Body(stages.PromotedValueControlFlow, method);
        var locals = lifted.DeclarationContext.LocalVariables;

        Assert.Collection(
            locals,
            local => Assert.Equal(ShaderType.I32, local.Type),
            local => Assert.Equal(ShaderType.I32, local.Type),
            local => Assert.Equal(ShaderType.F32, local.Type));
        Assert.NotEmpty(LocalMemory(lifted, locals[0]));
        Assert.Empty(LocalMemory(promoted, locals[0]));
        Assert.NotEmpty(LocalMemory(promoted, locals[1]));
        Assert.NotEmpty(LocalMemory(promoted, locals[2]));
        AssertEquivalent(RunValueCfg(lifted, []), RunValueCfg(promoted, []));
        Assert.Equal(new Value.Integer(16), RunValueCfg(promoted, []).Result);
    }

    [Fact]
    public void MissingMethodBodyFailsWithMethodContext()
    {
        var method = GetMethod(nameof(LocalDiamond));
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var environment = raw.Code.Environment;
        var bodylessEnvironment = new CilMethodEnvironment(
            environment.Method,
            null,
            environment.Parameters,
            environment.LocalVariables,
            environment.Offsets);
        var bodylessRaw = new RawCilFunctionBody(
            raw.Declaration,
            new LinearCode<CilInstructionInfo>(
                bodylessEnvironment,
                raw.Code.Instructions,
                CilStagePrettyPrinter.PrintRawLinearCode),
            raw.DeclarationContext.LocalVariables,
            raw.Symbols);
        var pre = new PreCilFunctionBody(bodylessRaw, Body(stages.Pre, method).Code);
        var labelled = new LabelledCilFunctionBody(pre, Body(stages.Labelled, method).Blocks);
        var shaderStack = new ShaderStackFunctionBody(labelled, Body(stages.ShaderStack, method).Blocks);
        var shaderControlFlow = new ShaderStackControlFlowBody(
            shaderStack,
            Body(stages.ShaderControlFlow, method).Graph);
        var value = new CilValueControlFlowBody(
            shaderControlFlow,
            Body(stages.ValueControlFlow, method).Graph);
        var module = new ShaderModuleDeclaration<CilValueControlFlowBody>(
            stages.ValueControlFlow.Declarations,
            ImmutableDictionary<FunctionDeclaration, CilValueControlFlowBody>.Empty.Add(value.Declaration, value));

        var exception = Assert.Throws<InvalidOperationException>(() => CilLocalPromotionPass.Run(module));

        Assert.Contains(method.Name, exception.Message);
        Assert.Contains("MethodBody", exception.Message);
    }

    [Fact]
    public void PublicClslCompilerReachesPromotedRegion()
    {
        var compiler = new CLSLCompiler(new(CLSLCompileTarget.IR));
        var raw = compiler.Parse(new LocalPromotionShader());
        var rawBody = Assert.Single(raw.FunctionDefinitions.Values,
            body => body.Declaration.Name == nameof(LocalPromotionShader.Promote));
        var compiled = compiler.Compile(raw);
        var body = Assert.Single(compiled.FunctionDefinitions.Values,
            candidate => candidate.Declaration.Name == nameof(LocalPromotionShader.Promote));

        Assert.DoesNotContain(
            body.Labels.SelectMany(label => body[label].Body.Elements),
            instruction => instruction.Operation is LoadOperation or StoreOperation &&
                           rawBody.DeclarationContext.LocalVariables.Any(local =>
                               ReferenceEquals(local.Value, instruction.Operand0)));
        Assert.Contains(body.Labels, label => !body[label].Parameters.IsEmpty);
        Assert.Equal(
            new Value.Integer(18),
            RunCfg(body, [new Value.Integer(2)]).Result);
    }

    [Fact]
    public void DeclarationBackedOraclesRejectMismatchedReturnTypes()
    {
        AssertReturnRejected(
            GetMethod(nameof(ReturnInt)),
            [new Value.Integer(7)],
            new Value.Integer(7),
            ShaderValue.Literal(new BoolLiteral(true)));
        AssertReturnRejected(
            GetMethod(nameof(ReturnBool)),
            [new Value.Boolean(true)],
            new Value.Boolean(true),
            ShaderValue.Literal(new I32Literal(1)));
        AssertReturnRejected(
            GetMethod(nameof(ReturnUInt)),
            [new Value.UnsignedInteger(7)],
            new Value.UnsignedInteger(7),
            ShaderValue.Literal(new I32Literal(7)));
        AssertReturnRejected(
            GetMethod(nameof(ReturnInt)),
            [new Value.Integer(7)],
            new Value.Integer(7),
            ShaderValue.Literal(new U32Literal(7)));
    }

    private static void AssertReturnRejected(
        MethodInfo method,
        ImmutableArray<Value> arguments,
        Value expected,
        IShaderValue wrongReturn)
    {
        var stages = CompilerTestPipeline.CompileStages(method);
        var value = Body(stages.ValueControlFlow, method);
        Assert.Equal(expected, RunValueCfg(value, arguments).Result);

        var wrongValue = WithReturn(value, wrongReturn);
        var module = new ShaderModuleDeclaration<CilValueControlFlowBody>(
            [wrongValue.Declaration],
            ImmutableDictionary<FunctionDeclaration, CilValueControlFlowBody>.Empty.Add(
                wrongValue.Declaration,
                wrongValue));
        var facts = Body(CilBlockControlFactsPass.Run(module), method);
        var region = Body(
            CilRegionPass.Run(CilBlockControlFactsPass.Run(module)),
            method);

        AssertReturnMismatch(() => RunValueCfg(wrongValue, arguments));
        AssertReturnMismatch(() => RunFactsCfg(facts, arguments));
        AssertReturnMismatch(() => RunCfg(region, arguments));
        AssertReturnMismatch(() => ScopedContinuationOracle.RunScoped(region, arguments));
    }

    private static void AssertReturnMismatch(Func<Execution> run) =>
        Assert.Contains(
            "return value does not match",
            Assert.Throws<NotSupportedException>(run).Message);

    private static CilValueControlFlowBody WithReturn(
        CilValueControlFlowBody body,
        IShaderValue wrongReturn)
    {
        var definitions = body.Graph.Labels().ToDictionary(
            label => label,
            label =>
            {
                var block = body.Graph[label];
                ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator =
                    block.Body.Last is Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>
                        ? Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(wrongReturn)
                        : block.Body.Last;
                var rewritten = block with
                {
                    Body = Seq.Create(block.Body.Elements, terminator)
                };
                return new ControlFlowGraph<CilValueBasicBlock>.NodeDefinition(
                    rewritten.Successor,
                    rewritten);
            });
        return new CilValueControlFlowBody(
            body.Source,
            new ControlFlowGraph<CilValueBasicBlock>(
                body.Graph.EntryLabel,
                definitions));
    }

    private void AssertPublicStages(
        MethodInfo method,
        ImmutableArray<Value> arguments,
        Value expected)
    {
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var lifted = Body(stages.ValueControlFlow, method);
        var promoted = Body(stages.PromotedValueControlFlow, method);
        var facts = Body(stages.ControlFacts, method);
        var region = Body(stages.Compiled, method);

        Assert.NotEmpty(raw.DeclarationContext.LocalVariables);
        Assert.NotEmpty(LocalMemory(lifted));
        Assert.Empty(LocalMemory(promoted));
        Assert.Same(lifted.Source, promoted.Source);
        Assert.Equal(
            raw.DeclarationContext.LocalVariables,
            promoted.DeclarationContext.LocalVariables);
        Assert.True(promoted.Graph.Labels().Sum(label => promoted.Graph[label].Parameters.Length) >
                    lifted.Graph.Labels().Sum(label => lifted.Graph[label].Parameters.Length));
        Assert.Same(promoted, facts.Source);

        var reference = Invoke(method, arguments);
        Assert.Equal(expected, reference);
        var before = RunValueCfg(lifted, arguments);
        Assert.Equal(reference, before.Result);
        AssertEquivalent(before, RunValueCfg(promoted, arguments));
        AssertEquivalent(before, RunFactsCfg(facts, arguments));
        AssertEquivalent(before, RunCfg(region, arguments));

        var lowered = new RegionParameterToLocalVariablePass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(region));
        AssertEquivalent(before, RunCfg(lowered, arguments));
        var source = ScalarControlFlowTests.Emit(lowered);
        AssertEquivalent(before, new EmittedScalarProgram(lowered, source).Run(arguments));

        var configuration = GetType().Assembly
                                     .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
                            ?? throw new InvalidOperationException("Missing assembly configuration.");
        output.WriteLine($"method={method}; configuration={configuration}; " +
                         $"InitLocals={raw.Code.Environment.Body?.InitLocals}; " +
                         $"locals=[{string.Join(", ", raw.DeclarationContext.LocalVariables.Select(
                             (local, index) => $"{index}:{local.Name}:{local.Type.Name}"))}]");
        output.WriteLine("=== before promotion ===");
        output.WriteLine(lifted.PrettyPrint());
        output.WriteLine("=== after promotion ===");
        output.WriteLine(promoted.PrettyPrint());
        output.WriteLine($"result={before.Result}; trace={string.Join(" -> ", before.Trace)}");
        output.WriteLine(source);
    }

    private static Value Invoke(MethodInfo method, ImmutableArray<Value> arguments)
    {
        var result = method.Invoke(null, arguments.Select(argument => argument switch
        {
            Value.Integer value => (object)value.Data,
            Value.Boolean value => value.Data,
            _ => throw new NotSupportedException($"Unsupported reflection argument {argument}.")
        }).ToArray());
        return result switch
        {
            int value => new Value.Integer(value),
            bool value => new Value.Boolean(value),
            _ => throw new NotSupportedException($"Unsupported reflection result {result}.")
        };
    }

    private static IEnumerable<Instruction<IShaderValue, IShaderValue>> LocalMemory(
        CilValueControlFlowBody body,
        VariableDeclaration? local = null) =>
        body.Graph.Labels()
            .SelectMany(label => body.Graph[label].Body.Elements)
            .Where(instruction =>
                instruction.Operation is LoadOperation or StoreOperation &&
                (local is null
                    ? body.DeclarationContext.LocalVariables.Any(candidate =>
                        ReferenceEquals(candidate.Value, instruction.Operand0))
                    : ReferenceEquals(local.Value, instruction.Operand0)));

    private static IEnumerable<IShaderValue> Values(CilValueControlFlowBody body) =>
        body.Graph.Labels().SelectMany(label =>
        {
            var block = body.Graph[label];
            return block.Parameters
                        .Concat(block.Body.Elements.SelectMany(instruction =>
                            instruction.Operands.Append(instruction.Result).OfType<IShaderValue>()))
                        .Concat(block.Body.Last.Evaluate(ValueTerminatorValues.Instance));
        });

    private static TBody Body<TBody>(
        ShaderModuleDeclaration<TBody> module,
        MethodBase method)
        where TBody : IFunctionBody =>
        Assert.Single(module.FunctionDefinitions, pair => pair.Key.Name == method.Name).Value;

    private static MethodInfo GetMethod(string name) =>
        typeof(CilLocalPromotionPassTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private sealed class ValueTerminatorValues
        : ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue, IEnumerable<IShaderValue>>
    {
        public static ValueTerminatorValues Instance { get; } = new();

        public IEnumerable<IShaderValue> ReturnVoid() => [];
        public IEnumerable<IShaderValue> ReturnExpr(IShaderValue expr) => [expr];
        public IEnumerable<IShaderValue> Br(RegionJump<IShaderValue> target) => target.Arguments;

        public IEnumerable<IShaderValue> BrIf(
            IShaderValue condition,
            RegionJump<IShaderValue> trueTarget,
            RegionJump<IShaderValue> falseTarget) =>
            [condition, .. trueTarget.Arguments, .. falseTarget.Arguments];
    }

    private static int LocalDiamond(bool choose, int left, int right)
    {
        int value;
        if (choose)
            value = left + 7;
        else
            value = right - 5;
        return value * 2;
    }

    private static int LocalLoop(int limit)
    {
        var sum = 0;
        for (var index = 0; index < limit; index++)
            sum += index;
        return sum;
    }

    private static int ReturnInt(int value) => value;
    private static bool ReturnBool(bool value) => value;
    private static uint ReturnUInt(uint value) => value;

    private sealed class LocalPromotionShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int Promote([Location(0)] int value)
        {
            var local = 7;
            if (value > 0)
                local += value;
            else
                local -= value;
            return local * 2;
        }
    }

    private static class EmittedFixtures
    {
        private static readonly Type FixtureType = BuildType();

        public const string InitIntName = "InitInt";
        public const string InitBoolName = "InitBool";
        public const string MissingDefinitionName = "MissingDefinition";
        public const string StorageBoundariesName = "StorageBoundaries";

        public static MethodInfo Method(string name) =>
            FixtureType.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} emitted fixture was not found.");

        private static Type BuildType()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("CilLocalPromotionFixtures"),
                AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("CilLocalPromotionFixtures")
                               .DefineType(
                                   "CilLocalPromotionFixtures",
                                   TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            DefineInitialRead(type, InitIntName, typeof(int), OpCodes.Ldloc_0);
            DefineInitialRead(type, InitBoolName, typeof(bool), OpCodes.Ldloc_0);
            DefineMissingDefinition(type);
            DefineStorageBoundaries(type);

            return type.CreateType()
                   ?? throw new InvalidOperationException("Failed to create local-promotion fixture type.");
        }

        private static void DefineInitialRead(
            TypeBuilder type,
            string name,
            Type resultType,
            OpCode load)
        {
            var method = Define(type, name, resultType);
            method.InitLocals = true;
            var il = method.GetILGenerator();
            _ = il.DeclareLocal(resultType);
            il.Emit(load);
            il.Emit(OpCodes.Ret);
        }

        private static void DefineMissingDefinition(TypeBuilder type)
        {
            var method = Define(type, MissingDefinitionName, typeof(int), typeof(bool));
            method.InitLocals = false;
            method.DefineParameter(1, ParameterAttributes.None, "assign");
            var il = method.GetILGenerator();
            var local = il.DeclareLocal(typeof(int));
            var load = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Brfalse_S, load);
            il.Emit(OpCodes.Ldc_I4_7);
            il.Emit(OpCodes.Stloc, local);
            il.MarkLabel(load);
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ret);
        }

        private static void DefineStorageBoundaries(TypeBuilder type)
        {
            var method = Define(type, StorageBoundariesName, typeof(int));
            method.InitLocals = true;
            var il = method.GetILGenerator();
            var promoted = il.DeclareLocal(typeof(int));
            var escaped = il.DeclareLocal(typeof(int));
            var unsupported = il.DeclareLocal(typeof(float));
            var observe = il.DefineLabel();
            il.Emit(OpCodes.Ldc_I4_7);
            il.Emit(OpCodes.Stloc, promoted);
            il.Emit(OpCodes.Ldc_I4_S, (sbyte)9);
            il.Emit(OpCodes.Stloc, escaped);
            il.Emit(OpCodes.Ldloca, escaped);
            il.Emit(OpCodes.Br_S, observe);
            il.MarkLabel(observe);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldc_R4, 1.0f);
            il.Emit(OpCodes.Stloc, unsupported);
            il.Emit(OpCodes.Ldloc, unsupported);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldloc, promoted);
            il.Emit(OpCodes.Ldloc, escaped);
            il.Emit(OpCodes.Add);
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
