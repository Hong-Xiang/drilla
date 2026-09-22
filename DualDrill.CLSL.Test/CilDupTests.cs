using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using Label = DualDrill.CLSL.Language.Symbol.Label;

namespace DualDrill.CLSL.Test;

public sealed class CilDupTests
{
    [Fact]
    public void ForcedProducerDupReusesOneSsaValueWithoutInventingAnInstruction()
    {
        var method = Fixtures.CallProduceDupAdd;
        var stages = CompilerTestPipeline.CompileStages(method);
        var labelled = Body(stages.Labelled, method);
        var shader = Body(stages.ShaderStack, method);
        var value = Body(stages.ValueControlFlow, method);
        var dup = Assert.Single(
            labelled.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        var duplicate = Assert.Single(
            shader.Blocks.Blocks.SelectMany(block => block.Body.Elements),
            instruction => instruction.Node is ShaderStackInstruction.Duplicate);

        Assert.Equal(dup.Index, duplicate.Annotation.Provenance.OriginalIndex);
        Assert.Contains("duplicate", shader.PrettyPrint());
        Assert.Collection(
            duplicate.Annotation.Pre,
            type => Assert.Same(ShaderType.I32, type));
        Assert.Collection(
            duplicate.Annotation.Post,
            first => Assert.Same(ShaderType.I32, first),
            second => Assert.Same(ShaderType.I32, second));

        var instructions = Instructions(value);
        var call = Assert.Single(
            instructions,
            instruction => instruction.Operation is CallOperation);
        var add = Assert.Single(
            instructions,
            instruction => instruction.Operation is
                NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>);
        Assert.Same(call.Result, add.Operand0);
        Assert.Same(call.Result, add.Operand1);
        Assert.DoesNotContain(
            instructions,
            instruction => instruction.Payload is ShaderStackProvenance provenance &&
                           provenance.OriginalIndex == dup.Index);
        Assert.Equal(10, method.Invoke(null, [4]));
    }

    [Fact]
    public void AssignmentExpressionCapturesRealCompilerDupInThisConfiguration()
    {
        var method = GetMethod(nameof(AssignmentExpressionDup));
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method).Code.Instructions;

        Assert.Contains(raw, instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        Assert.Equal(10, AssignmentExpressionDup(4));
        Assert.NotNull(Body(stages.Compiled, method));
    }

    [Fact]
    public void RetainedPrefixAndFloatTopAreDuplicatedInExactOrder()
    {
        var method = Fixtures.RetainedPrefixFloat;
        var stages = CompilerTestPipeline.CompileStages(method);
        var labelled = Body(stages.Labelled, method);
        var dup = Assert.Single(
            labelled.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        var before = Pre(labelled, dup.Index).Types.ToArray();
        var after = Pre(labelled, dup.Index + 1).Types.ToArray();

        Assert.Collection(
            before,
            top => Assert.IsType<CilStackType.Float32>(top),
            prefix => Assert.IsType<CilStackType.Int32>(prefix));
        Assert.Collection(
            after,
            first => Assert.Same(before[0], first),
            second => Assert.Same(before[0], second),
            prefix => Assert.Same(before[1], prefix));

        var duplicate = Assert.Single(
            Body(stages.ShaderStack, method).Blocks.Blocks.SelectMany(block => block.Body.Elements),
            instruction => instruction.Node is ShaderStackInstruction.Duplicate);
        Assert.Collection(
            duplicate.Annotation.Pre,
            prefix => Assert.Same(ShaderType.I32, prefix),
            top => Assert.Same(ShaderType.F32, top));
        Assert.Collection(
            duplicate.Annotation.Post,
            prefix => Assert.Same(ShaderType.I32, prefix),
            first => Assert.Same(ShaderType.F32, first),
            second => Assert.Same(ShaderType.F32, second));
    }

    [Fact]
    public void PointerDupAcrossEdgePreservesExactTypeAndRootIdentity()
    {
        var method = Fixtures.PointerDupEdge;
        var stages = CompilerTestPipeline.CompileStages(method);
        var labelled = Body(stages.Labelled, method);
        var dup = Assert.Single(
            labelled.RawCode.Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        var before = Assert.IsType<CilStackType.ManagedPointer>(
            Assert.Single(Pre(labelled, dup.Index).Types));
        Assert.Collection(
            Pre(labelled, dup.Index + 1).Types,
            first => Assert.Same(before, first),
            second => Assert.Same(before, second));

        var value = Body(stages.ValueControlFlow, method);
        var jump = Assert.Single(
            value.Graph.Labels()
                 .Select(label => value.Graph[label].Body.Last)
                 .OfType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(),
            branch => branch.Target.Arguments.Length == 2);
        var local = Assert.Single(value.DeclarationContext.LocalVariables).Value;
        Assert.Collection(
            jump.Target.Arguments,
            first => Assert.Same(local, first),
            second => Assert.Same(local, second));
    }

    [Fact]
    public void ConditionalBranchConsumesOneCopyAndCarriesTheOtherOnBothOrderedEdges()
    {
        var method = Fixtures.BranchCarriesDup;
        var value = CompilerTestPipeline.ValueControlFlow(method);
        var branch = Assert.Single(
            value.Graph.Labels()
                 .Select(label => value.Graph[label].Body.Last)
                 .OfType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>());
        var load = Assert.Single(
            Instructions(value),
            instruction => instruction.Operation is LoadOperation);

        Assert.Same(load.Result, Assert.Single(branch.TrueTarget.Arguments));
        Assert.Same(load.Result, Assert.Single(branch.FalseTarget.Arguments));

        var zero = RunValueCfg(value, [new Value.Integer(0)]);
        var nonzero = RunValueCfg(value, [new Value.Integer(7)]);
        Assert.Equal(new Value.Integer(0), zero.Result);
        Assert.Equal(new Value.Integer(7), nonzero.Result);
        Assert.Same(branch.FalseTarget.Label, zero.Trace[^1]);
        Assert.Same(branch.TrueTarget.Label, nonzero.Trace[^1]);
    }

    [Fact]
    public void VectorDupIsAcceptedWithoutNormalization()
    {
        var method = Fixtures.VectorDup;
        var stages = CompilerTestPipeline.CompileStages(method);
        var shader = Body(stages.ShaderStack, method);
        var duplicate = Assert.Single(
            shader.Blocks.Blocks.SelectMany(block => block.Body.Elements),
            instruction => instruction.Node is ShaderStackInstruction.Duplicate);

        Assert.Collection(
            duplicate.Annotation.Pre,
            type => Assert.Same(ShaderType.Vec2F32, type));
        Assert.Collection(
            duplicate.Annotation.Post,
            first => Assert.Same(ShaderType.Vec2F32, first),
            second => Assert.Same(ShaderType.Vec2F32, second));
    }

    [Fact]
    public void WideScalarDupPreservesCanonicalTypes()
    {
        AssertDuplicateType(Fixtures.Int64Dup, ShaderType.I64);
        AssertDuplicateType(Fixtures.Float64Dup, ShaderType.F64);
    }

    [Theory]
    [InlineData(nameof(Fixtures.UnderflowDup), "CIL evaluation stack underflow")]
    [InlineData(nameof(Fixtures.ObjectDup), "dup does not support object references")]
    [InlineData(nameof(Fixtures.ArrayDup), "dup does not support object references")]
    public void InvalidDupInputsAreRejectedAtTheOpcode(string methodName, string expected)
    {
        var method = Fixtures.Method(methodName);
        var dup = Assert.Single(
            CilMethodDecoder.Decode(method).Instructions,
            instruction => instruction.Instruction.OpCode == OpCodes.Dup);
        var exception = Assert.Throws<ValidationException>(() =>
            CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)));

        Assert.Contains(expected, exception.Message);
        Assert.Contains($"IL_{dup.ByteOffset:X4} (dup)", exception.Message);
        Assert.Contains(method.Name, exception.Message);
    }

    [Fact]
    public void ShaderStackDuplicateRejectsEmptyAndForgedTransitions()
    {
        var duplicate = new ShaderStackInstruction.Duplicate();
        Assert.Contains(
            "empty shader stack",
            Assert.Throws<ArgumentException>(() =>
                ShaderStackValidation.Apply(duplicate, [])).Message);

        var exact = ShaderStackValidation.Apply(duplicate, [ShaderType.F32]);
        Assert.Collection(
            exact,
            first => Assert.Same(ShaderType.F32, first),
            second => Assert.Same(ShaderType.F32, second));

        var shader = CompilerTestPipeline.ShaderStack(Fixtures.CallProduceDupAdd);
        var source = shader.Source.RawCode.Instructions.Single(instruction =>
            instruction.Instruction.OpCode == OpCodes.Dup);
        var provenance = ShaderStackProvenance.Source(source, 0);
        var returned = Terminator.B.ReturnExpr<Label, ShaderStackOperand>(
            ShaderStackOperand.At(0, ShaderType.I32));
        var exception = Assert.Throws<ArgumentException>(() =>
            new ShaderStackBasicBlock(
                Label.Create("forged-dup"),
                [ShaderType.I32],
                Seq.Create([
                    new Annotated<ShaderStackInstruction, ShaderStackTransition>(
                        duplicate,
                        new ShaderStackTransition(
                            [ShaderType.I32],
                            [ShaderType.I32],
                            provenance),
                        CilStagePrettyPrinter.PrintShaderStackInstruction)
                ], new Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition>(
                    returned,
                    new ShaderStackTransition([ShaderType.I32], [], provenance),
                    CilStagePrettyPrinter.PrintShaderStackTerminator))));

        Assert.Contains("Post does not match", exception.Message);
    }

    private static int Produce(int value) => value + 1;

    private static int AssignmentExpressionDup(int input)
    {
        int assigned;
        return (assigned = Produce(input)) + assigned;
    }

    private static MethodInfo GetMethod(string name) =>
        typeof(CilDupTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static ImmutableArray<Instruction<IShaderValue, IShaderValue>> Instructions(
        CilValueControlFlowBody body) =>
        [.. body.Graph.Labels().SelectMany(label => body.Graph[label].Body.Elements)];

    private static PreStack Pre(LabelledCilFunctionBody body, int instructionIndex) =>
        Assert.Single(
            body.PreAnnotatedCode.Instructions,
            instruction => instruction.Node.Index == instructionIndex).Annotation;

    private static void AssertDuplicateType(MethodInfo method, IShaderType expected)
    {
        var duplicate = Assert.Single(
            CompilerTestPipeline.ShaderStack(method).Blocks.Blocks.SelectMany(block => block.Body.Elements),
            instruction => instruction.Node is ShaderStackInstruction.Duplicate);
        Assert.Collection(
            duplicate.Annotation.Pre,
            type => Assert.Same(expected, type));
        Assert.Collection(
            duplicate.Annotation.Post,
            first => Assert.Same(expected, first),
            second => Assert.Same(expected, second));
    }

    private static TBody Body<TBody>(
        ShaderModuleDeclaration<TBody> module,
        MethodBase method)
        where TBody : IFunctionBody =>
        Assert.Single(
            module.FunctionDefinitions,
            pair => pair.Key.Name == method.Name).Value;

    private static class Fixtures
    {
        private static readonly Type Type = Build();

        public static MethodInfo CallProduceDupAdd => Method(nameof(CallProduceDupAdd));
        public static MethodInfo RetainedPrefixFloat => Method(nameof(RetainedPrefixFloat));
        public static MethodInfo PointerDupEdge => Method(nameof(PointerDupEdge));
        public static MethodInfo BranchCarriesDup => Method(nameof(BranchCarriesDup));
        public static MethodInfo VectorDup => Method(nameof(VectorDup));
        public static MethodInfo Int64Dup => Method(nameof(Int64Dup));
        public static MethodInfo Float64Dup => Method(nameof(Float64Dup));
        public static MethodInfo UnderflowDup => Method(nameof(UnderflowDup));
        public static MethodInfo ObjectDup => Method(nameof(ObjectDup));
        public static MethodInfo ArrayDup => Method(nameof(ArrayDup));

        public static MethodInfo Method(string name) =>
            Type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} emitted fixture was not found.");

        private static Type Build()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("CilDupFixtures"),
                AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("CilDupFixtures").DefineType(
                "CilDupFixtures",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var produce = Define(type, "Produce", typeof(int), ("value", typeof(int)));
            var produceIl = produce.GetILGenerator();
            produceIl.Emit(OpCodes.Ldarg_0);
            produceIl.Emit(OpCodes.Ldc_I4_1);
            produceIl.Emit(OpCodes.Add);
            produceIl.Emit(OpCodes.Ret);

            var call = Define(type, nameof(CallProduceDupAdd), typeof(int), ("value", typeof(int)));
            var callIl = call.GetILGenerator();
            callIl.Emit(OpCodes.Ldarg_0);
            callIl.Emit(OpCodes.Call, produce);
            callIl.Emit(OpCodes.Dup);
            callIl.Emit(OpCodes.Add);
            callIl.Emit(OpCodes.Ret);

            var prefix = Define(type, nameof(RetainedPrefixFloat), typeof(int), ("value", typeof(float)));
            var prefixIl = prefix.GetILGenerator();
            prefixIl.Emit(OpCodes.Ldc_I4_7);
            prefixIl.Emit(OpCodes.Ldarg_0);
            prefixIl.Emit(OpCodes.Dup);
            prefixIl.Emit(OpCodes.Pop);
            prefixIl.Emit(OpCodes.Pop);
            prefixIl.Emit(OpCodes.Ret);

            var pointer = Define(type, nameof(PointerDupEdge), typeof(int));
            var pointerIl = pointer.GetILGenerator();
            var local = pointerIl.DeclareLocal(typeof(int));
            var pointerTarget = pointerIl.DefineLabel();
            pointerIl.Emit(OpCodes.Ldloca, local);
            pointerIl.Emit(OpCodes.Dup);
            pointerIl.Emit(OpCodes.Br, pointerTarget);
            pointerIl.MarkLabel(pointerTarget);
            pointerIl.Emit(OpCodes.Pop);
            pointerIl.Emit(OpCodes.Pop);
            pointerIl.Emit(OpCodes.Ldc_I4_0);
            pointerIl.Emit(OpCodes.Ret);

            var branch = Define(type, nameof(BranchCarriesDup), typeof(int), ("value", typeof(int)));
            var branchIl = branch.GetILGenerator();
            var trueTarget = branchIl.DefineLabel();
            branchIl.Emit(OpCodes.Ldarg_0);
            branchIl.Emit(OpCodes.Dup);
            branchIl.Emit(OpCodes.Brtrue, trueTarget);
            branchIl.Emit(OpCodes.Ret);
            branchIl.MarkLabel(trueTarget);
            branchIl.Emit(OpCodes.Ret);

            var vector = Define(type, nameof(VectorDup), typeof(vec2f32), ("value", typeof(vec2f32)));
            var vectorIl = vector.GetILGenerator();
            vectorIl.Emit(OpCodes.Ldarg_0);
            vectorIl.Emit(OpCodes.Dup);
            vectorIl.Emit(OpCodes.Pop);
            vectorIl.Emit(OpCodes.Ret);

            EmitIdentityDup(Define(type, nameof(Int64Dup), typeof(long), ("value", typeof(long))));
            EmitIdentityDup(Define(type, nameof(Float64Dup), typeof(double), ("value", typeof(double))));

            var underflow = Define(type, nameof(UnderflowDup), typeof(void));
            var underflowIl = underflow.GetILGenerator();
            underflowIl.Emit(OpCodes.Dup);
            underflowIl.Emit(OpCodes.Ret);

            EmitObjectDup(Define(type, nameof(ObjectDup), typeof(int), ("value", typeof(object))));
            EmitObjectDup(Define(type, nameof(ArrayDup), typeof(int), ("value", typeof(int[]))));

            return type.CreateType()
                   ?? throw new InvalidOperationException("Failed to create CIL dup fixtures.");
        }

        private static void EmitObjectDup(MethodBuilder method)
        {
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
        }

        private static void EmitIdentityDup(MethodBuilder method)
        {
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        }

        private static MethodBuilder Define(
            TypeBuilder type,
            string name,
            Type result,
            params (string Name, Type Type)[] parameters)
        {
            var method = type.DefineMethod(
                name,
                MethodAttributes.Public | MethodAttributes.Static,
                result,
                parameters.Select(parameter => parameter.Type).ToArray());
            foreach (var (index, parameter) in parameters.Index())
                method.DefineParameter(index + 1, ParameterAttributes.None, parameter.Name);
            return method;
        }
    }
}
