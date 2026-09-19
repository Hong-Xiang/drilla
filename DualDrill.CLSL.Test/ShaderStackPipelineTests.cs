using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.Transform;
using DualDrill.Common.Nat;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using Label = DualDrill.CLSL.Language.Symbol.Label;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class ShaderStackPipelineTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("bool-merge")]
    [InlineData("compound")]
    [InlineData("loop")]
    public void CaptureActualPipelineStages(string fixture)
    {
        var method = fixture switch
        {
            "bool-merge" => Fixtures.BoolZeroDiamond,
            "compound" => GetMethod(nameof(Compound)),
            "loop" => GetMethod(nameof(CountdownSum)),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture))
        };
        var stages = CompilerTestPipeline.CompileStages(method);
        var configuration = GetType().Assembly
                                     .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;

        output.WriteLine($"fixture={fixture}");
        output.WriteLine($"configuration={configuration}");
        output.WriteLine($"method={method.Module.ModuleVersionId}:{method.MetadataToken}:{method}");
        output.WriteLine("=== raw ===");
        output.WriteLine(CompilerTestPipeline.RawBody(stages.Raw, method).PrettyPrint());
        output.WriteLine("=== pre ===");
        output.WriteLine(Body(stages.Pre, method).PrettyPrint());
        output.WriteLine("=== labelled-cil ===");
        output.WriteLine(Body(stages.Labelled, method).PrettyPrint());
        output.WriteLine("=== shader-stack-blocks ===");
        output.WriteLine(Body(stages.ShaderStack, method).PrettyPrint());
        output.WriteLine("=== shader-stack-cfg ===");
        output.WriteLine(Body(stages.ShaderControlFlow, method).PrettyPrint());
        output.WriteLine("=== value-cfg ===");
        output.WriteLine(Body(stages.ValueControlFlow, method).PrettyPrint());
        output.WriteLine("=== promoted-value-cfg ===");
        output.WriteLine(Body(stages.PromotedValueControlFlow, method).PrettyPrint());
        output.WriteLine("=== slang ===");
        output.WriteLine(ScalarControlFlowTests.Emit(Body(stages.Compiled, method)));

        Assert.Contains("linear-cil raw", CompilerTestPipeline.RawBody(stages.Raw, method).PrettyPrint());
        Assert.Contains("labelled-shader-stack-block-list", Body(stages.ShaderStack, method).PrettyPrint());
        Assert.Contains("flat-value-cfg", Body(stages.ValueControlFlow, method).PrettyPrint());
        Assert.Contains("flat-value-cfg", Body(stages.PromotedValueControlFlow, method).PrettyPrint());
    }

    [Fact]
    public void BoolZeroDiamondPublishesTypedExpansionsAndProvenanceBeforeCfg()
    {
        var method = Fixtures.BoolZeroDiamond;
        var stages = CompilerTestPipeline.CompileStages(method);
        var labelled = Body(stages.Labelled, method);
        var shader = Body(stages.ShaderStack, method);
        var shaderCfg = Body(stages.ShaderControlFlow, method);
        var values = Body(stages.ValueControlFlow, method);
        var boolLoad = labelled.RawCode.Instructions.First(instruction =>
            instruction.Instruction.OpCode == OpCodes.Ldarg_0 &&
            labelled.PreAnnotatedCode.Instructions.Any(row => row.Node.Index == instruction.Index));
        var branch = labelled.RawCode.Instructions.Single(instruction =>
            instruction.Instruction.OpCode == OpCodes.Brtrue_S);

        var loadExpansion = shader.Blocks.Blocks.SelectMany(block => block.Body.Elements)
                                  .Where(item => item.Annotation.Provenance.OriginalIndex == boolLoad.Index)
                                  .ToArray();
        Assert.Collection(
            loadExpansion,
            load =>
            {
                var operation = Assert.IsType<ShaderStackInstruction.Operation>(load.Node);
                Assert.IsType<LoadOperation>(operation.Instruction.Operation);
                Assert.Empty(load.Annotation.Pre);
                Assert.Collection(load.Annotation.Post, type => Assert.Equal(ShaderType.Bool, type));
                Assert.Equal(0, load.Annotation.Provenance.ExpansionOrdinal);
            },
            normalize =>
            {
                AssertConversion<BoolType, IntType<N32>>(normalize);
                Assert.Collection(normalize.Annotation.Pre, type => Assert.Equal(ShaderType.Bool, type));
                Assert.Collection(normalize.Annotation.Post, type => Assert.Equal(ShaderType.I32, type));
                Assert.Equal(1, normalize.Annotation.Provenance.ExpansionOrdinal);
            });

        var branchBlock = shader.Blocks.Blocks.Single(block =>
            block.Body.Last.Annotation.Provenance.OriginalIndex == branch.Index);
        var truth = Assert.Single(branchBlock.Body.Elements,
            item => item.Annotation.Provenance.OriginalIndex == branch.Index);
        AssertConversion<IntType<N32>, BoolType>(truth);
        Assert.Collection(truth.Annotation.Pre, type => Assert.Equal(ShaderType.I32, type));
        Assert.Collection(truth.Annotation.Post, type => Assert.Equal(ShaderType.Bool, type));
        Assert.Empty(branchBlock.Body.Last.Annotation.Post);
        Assert.IsType<Terminator.D.BrIf<Label, ShaderStackOperand>>(branchBlock.Body.Last.Node);

        Assert.Same(labelled.Blocks.EntryLabel, shader.Blocks.EntryLabel);
        Assert.Same(shader.Blocks.EntryLabel, shaderCfg.Graph.EntryLabel);
        Assert.Equal(labelled.Labels, shader.Blocks.Blocks.Select(block => block.Label));
        Assert.Equal(labelled.Labels.ToHashSet(), shaderCfg.Graph.Labels().ToHashSet());
        Assert.Equal(labelled.Labels.ToHashSet(), values.Graph.Labels().ToHashSet());

        var payloads = values.Graph.Labels()
                             .SelectMany(label => values.Graph[label].Body.Elements)
                             .Select(instruction => instruction.Payload)
                             .OfType<ShaderStackProvenance>();
        Assert.Contains(payloads, payload =>
            payload.OriginalIndex == boolLoad.Index && payload.ExpansionOrdinal == 1);
        Assert.DoesNotContain(
            shader.Blocks.Blocks.SelectMany(block => block.Body.Elements)
                  .SelectMany(item => item.Node is ShaderStackInstruction.Operation operation
                      ? operation.Instruction.Operands
                      : []),
            operand => operand is ShaderStackOperand.Immediate { Value: IntermediateValue });
        foreach (var block in shader.Blocks.Blocks.Where(block =>
                     block.Body.Last.Annotation.Provenance.Synthetic))
        {
            var source = labelled[block.Label].Instructions[^1].Node;
            Assert.Equal(source.Index, block.Body.Last.Annotation.Provenance.OriginalIndex);
            Assert.Equal(source.ByteOffset, block.Body.Last.Annotation.Provenance.ByteStart);
            Assert.Equal(source.NextByteOffset, block.Body.Last.Annotation.Provenance.ByteEnd);
        }
    }

    [Theory]
    [InlineData(false, 29)]
    [InlineData(true, 11)]
    public void BoolZeroDiamondMatchesCpuAcrossOriginalLoweredAndEmittedStages(bool flag, int expected) =>
        CheckSemanticStages(Fixtures.BoolZeroDiamond, [new Value.Boolean(flag)], new Value.Integer(expected));

    [Fact]
    public async Task BuriedBoolCallAdapterAndUnitCallAreExplicitShaderInstructions()
    {
        var method = GetMethod(nameof(CallBoolAndObserve));
        var shader = CompilerTestPipeline.ShaderStack(method);
        var operations = shader.Blocks.Blocks.SelectMany(block => block.Body.Elements)
                               .Select(item => item.Node)
                               .OfType<ShaderStackInstruction.Operation>()
                               .ToArray();
        var calls = operations.Where(operation => operation.Instruction.Operation is CallOperation).ToArray();
        var boolCall = Assert.Single(calls, call =>
            call.Instruction.Operands.First() is ShaderStackOperand.Immediate
            {
                Value: FunctionDeclaration { Name: nameof(BoolInt) }
            });
        var boolConversion = Assert.Single(operations, operation =>
            operation.Instruction.Operation is ScalarConversionOperation<IntType<N32>, BoolType> &&
            operation.Instruction.Operands.Single() is ShaderStackOperand.Depth { Index: 1 });

        Assert.Equal(0, boolConversion.PopCount);
        Assert.Equal(3, boolCall.PopCount);
        Assert.Collection(
            boolCall.Instruction.Operands.Skip(1),
            operand => Assert.Equal(0, Assert.IsType<ShaderStackOperand.Depth>(operand).Index),
            operand => Assert.Equal(1, Assert.IsType<ShaderStackOperand.Depth>(operand).Index));

        var unitCall = Assert.Single(calls, call =>
            call.Instruction.Operands.First() is ShaderStackOperand.Immediate
            {
                Value: FunctionDeclaration { Name: nameof(Observe) }
            });
        Assert.IsType<UnitType>(unitCall.Instruction.Result);
        Assert.Equal(1, unitCall.PopCount);

        var value = CompilerTestPipeline.ValueControlFlow(method);
        Assert.Contains(
            value.Graph.Labels().SelectMany(label => value.Graph[label].Body.Elements),
            instruction => instruction.Operation is CallOperation && instruction.Result?.Type is UnitType);
        var lowered = CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method))
                                       .RunPass(new FunctionToOperationPass())
                                       .RunPass(new StablePointerRegionParameterPass());
        var slang = new SlangEmitter(new SlangTargetLowering().Lower(lowered)).Emit();
        Assert.Contains("Observe(", slang);
        Assert.DoesNotContain(": void =", slang);
        await new SlangService().ValidateAsync(slang);
        var publicSlang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(new UnitCallShader());
        Assert.Contains("Observe(", publicSlang);
        Assert.DoesNotContain(": void =", publicSlang);
        await new SlangService().ValidateAsync(publicSlang);
        Assert.Equal(12, CallBoolAndObserve(true, 7));
        Assert.Equal(7, CallBoolAndObserve(false, 7));
    }

    [Fact]
    public void FieldStoreKeepsOwnerAddressSpaceAndConvertsBuriedBool()
    {
        var shader = CompilerTestPipeline.ShaderStack(GetMethod(nameof(StoreFlag)));
        var storeIndex = shader.Source.RawCode.Instructions.Single(instruction =>
            instruction.Instruction.OpCode == OpCodes.Stfld).Index;
        var expansion = shader.Blocks.Blocks.SelectMany(block => block.Body.Elements)
                              .Where(item => item.Annotation.Provenance.OriginalIndex == storeIndex)
                              .ToArray();
        var operations = expansion.Select(item => item.Node)
                               .OfType<ShaderStackInstruction.Operation>()
                               .ToArray();
        var address = Assert.Single(operations,
            operation => operation.Instruction.Operation is AddressOfMemberOperation);
        var owner = Assert.IsType<ShaderStackOperand.Depth>(Assert.Single(address.Instruction.Operands));
        var ownerType = Assert.IsAssignableFrom<IPtrType>(owner.Type);
        var resultType = Assert.IsAssignableFrom<IPtrType>(address.Instruction.Result);
        Assert.Equal(1, owner.Index);
        Assert.Equal(ownerType.AddressSpace, resultType.AddressSpace);
        Assert.Equal(0, address.PopCount);

        var conversion = Assert.Single(operations, operation =>
            operation.Instruction.Operation is ScalarConversionOperation<IntType<N32>, BoolType> &&
            operation.PopCount == 0 &&
            operation.Instruction.Operands.Single() is ShaderStackOperand.Depth { Index: 1 });
        Assert.Equal(1, Assert.IsType<ShaderStackOperand.Depth>(
            Assert.Single(conversion.Instruction.Operands)).Index);
        Assert.Equal(0, conversion.PopCount);
        Assert.Equal(4, Assert.Single(operations,
            operation => operation.Instruction.Operation is StoreOperation).PopCount);
    }

    [Fact]
    public async Task UnsignedArithmeticAndUnorderedFloatRelationsUseExplicitAdapters()
    {
        var unsigned = CompilerTestPipeline.ShaderStack(Fixtures.UnsignedDivide);
        var unsignedOperations = Operations(unsigned);
        Assert.True(unsignedOperations.Count(operation =>
            operation.Instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>>) >= 2);
        Assert.Contains(unsignedOperations, operation =>
            operation.Instruction.Operation is NumericBinaryArithmeticOperation<
                UIntType<N32>, BinaryArithmetic.Div>);
        Assert.Contains(unsignedOperations, operation =>
            operation.Instruction.Operation is ScalarConversionOperation<UIntType<N32>, IntType<N32>>);

        var unordered = CompilerTestPipeline.ShaderStack(Fixtures.UnorderedLessThan);
        var unorderedOperations = Operations(unordered);
        Assert.Contains(unorderedOperations, operation =>
            operation.Instruction.Operation is NumericBinaryRelationalOperation<
                FloatType<N64>, BinaryRelational.Ge>);
        Assert.Contains(unorderedOperations, operation =>
            operation.Instruction.Operation is LogicalNotOperation);

        var notEqual = CompilerTestPipeline.ShaderStack(Fixtures.UnorderedNotEqual);
        Assert.Contains(Operations(notEqual), operation =>
            operation.Instruction.Operation is NumericBinaryRelationalOperation<
                FloatType<N64>, BinaryRelational.Ne>);
        Assert.DoesNotContain(Operations(notEqual), operation =>
            operation.Instruction.Operation is LogicalNotOperation);

        var unsignedBranch = CompilerTestPipeline.ShaderStack(Fixtures.UnsignedGreater);
        var unsignedBranchOperations = Operations(unsignedBranch);
        Assert.Equal(2, unsignedBranchOperations.Count(operation =>
            operation.Instruction.Operation is ScalarConversionOperation<IntType<N32>, UIntType<N32>>));
        Assert.Contains(unsignedBranchOperations, operation =>
            operation.Instruction.Operation is NumericBinaryRelationalOperation<
                UIntType<N32>, BinaryRelational.Gt>);

        AssertValue(
            Fixtures.UnsignedDivide,
            [new Value.UnsignedInteger(uint.MaxValue), new Value.UnsignedInteger(2)],
            new Value.UnsignedInteger(uint.MaxValue / 2));
        AssertValue(
            Fixtures.UnsignedRemainder,
            [new Value.UnsignedInteger(uint.MaxValue), new Value.UnsignedInteger(2)],
            new Value.UnsignedInteger(uint.MaxValue % 2));
        AssertValue(
            Fixtures.UnsignedRemainder,
            [new Value.UnsignedInteger(0x80000000), new Value.UnsignedInteger(3)],
            new Value.UnsignedInteger(0x80000000 % 3));

        var unsignedRelations = new (MethodInfo Method, Func<uint, uint, bool> Reference)[]
        {
            (Fixtures.UnsignedGreater, static (left, right) => left > right),
            (Fixtures.UnsignedGreaterOrEqual, static (left, right) => left >= right),
            (Fixtures.UnsignedLess, static (left, right) => left < right),
            (Fixtures.UnsignedLessOrEqual, static (left, right) => left <= right)
        };
        var unsignedInputs = new (uint Left, uint Right)[]
        {
            (0, uint.MaxValue),
            (uint.MaxValue, 0),
            (0x80000000, 0x7fffffff),
            (uint.MaxValue, uint.MaxValue)
        };
        foreach (var (method, reference) in unsignedRelations)
            foreach (var (left, right) in unsignedInputs)
                AssertValue(
                    method,
                    [new Value.UnsignedInteger(left), new Value.UnsignedInteger(right)],
                    new Value.Integer(reference(left, right) ? 1 : 0));

        var f64Relations = new (MethodInfo Method, Func<double, double, bool> Reference)[]
        {
            (Fixtures.UnorderedGreaterF64, static (left, right) => Unordered(left, right) || left > right),
            (Fixtures.UnorderedGreaterOrEqualF64, static (left, right) => Unordered(left, right) || left >= right),
            (Fixtures.UnorderedLessF64, static (left, right) => Unordered(left, right) || left < right),
            (Fixtures.UnorderedLessOrEqualF64, static (left, right) => Unordered(left, right) || left <= right),
            (Fixtures.UnorderedNotEqual, static (left, right) => left != right)
        };
        var f64Inputs = new (double Left, double Right)[]
        {
            (double.NaN, 0),
            (0, double.NaN),
            (double.NaN, double.NaN),
            (-1, 1),
            (1, -1),
            (3, 3),
            (double.NegativeInfinity, double.PositiveInfinity),
            (double.PositiveInfinity, double.NegativeInfinity)
        };
        foreach (var (method, reference) in f64Relations)
            foreach (var (left, right) in f64Inputs)
                AssertValue(
                    method,
                    [new Value.Float64(left), new Value.Float64(right)],
                    new Value.Integer(reference(left, right) ? 1 : 0));
        foreach (var (left, right) in f64Inputs)
            AssertValue(
                Fixtures.UnorderedLessThan,
                [new Value.Float64(left), new Value.Float64(right)],
                new Value.Integer(Unordered(left, right) || left < right ? 1 : 0));

        foreach (var (left, right) in new (float Left, float Right)[]
                 {
                     (float.NaN, 0),
                     (0, float.NaN),
                     (float.NaN, float.NaN),
                     (-1, 1),
                     (1, -1),
                     (float.NegativeInfinity, float.PositiveInfinity)
                 })
        {
            AssertValue(
                Fixtures.UnorderedLessF32,
                [new Value.Float32(left), new Value.Float32(right)],
                new Value.Integer(Unordered(left, right) || left < right ? 1 : 0));
            AssertValue(
                Fixtures.UnorderedGreaterExpressionF32,
                [new Value.Float32(left), new Value.Float32(right)],
                new Value.Integer(Unordered(left, right) || left > right ? 1 : 0));
        }

        await new SlangService().ValidateAsync(Emit(Fixtures.UnsignedRemainder));
        await new SlangService().ValidateAsync(Emit(Fixtures.UnorderedLessF32));
    }

    [Fact]
    public void ReturnBoundaryConvertsCanonicalBoolAndNativeComparisonFeedsBranchDirectly()
    {
        var returned = CompilerTestPipeline.ShaderStack(GetMethod(nameof(ReturnBool)));
        var returnBlock = returned.Blocks.Blocks.Single(block =>
            block.Body.Last.Node is Terminator.D.ReturnExpr<Label, ShaderStackOperand>);
        AssertConversion<IntType<N32>, BoolType>(returnBlock.Body.Elements.Last());
        var returnValue = Assert.IsType<Terminator.D.ReturnExpr<Label, ShaderStackOperand>>(
            returnBlock.Body.Last.Node).Expr;
        Assert.Equal(ShaderType.Bool, returnValue.Type);

        var native = CompilerTestPipeline.ShaderStack(Fixtures.NativeGreater);
        var nativeSource = native.Source.RawCode.Instructions.Single(instruction =>
            instruction.Instruction.OpCode == OpCodes.Bgt_S);
        var nativeBlock = native.Blocks.Blocks.Single(block =>
            block.Body.Last.Annotation.Provenance.OriginalIndex == nativeSource.Index);
        var condition = Assert.IsType<Terminator.D.BrIf<Label, ShaderStackOperand>>(
            nativeBlock.Body.Last.Node).Condition;
        Assert.Equal(ShaderType.Bool, condition.Type);
        var comparison = Assert.Single(nativeBlock.Body.Elements,
            item => item.Annotation.Provenance.OriginalIndex == nativeSource.Index);
        Assert.IsType<NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>>(
            Assert.IsType<ShaderStackInstruction.Operation>(comparison.Node).Instruction.Operation);
    }

    [Theory]
    [InlineData(-1, 3, false, 11)]
    [InlineData(-1, 3, true, 29)]
    [InlineData(3, 3, false, 11)]
    [InlineData(3, 3, true, 29)]
    [InlineData(8, 3, false, 29)]
    [InlineData(8, 3, true, 11)]
    public void CompoundPredicateMatchesCpuAcrossOriginalLoweredAndEmittedStages(
        int left,
        int right,
        bool gate,
        int expected)
    {
        var method = GetMethod(nameof(Compound));
        Assert.Equal(expected, Compound(left, right, gate));
        CheckSemanticStages(
            method,
            [new Value.Integer(left), new Value.Integer(right), new Value.Boolean(gate)],
            new Value.Integer(expected));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 15)]
    public void CountdownSumMatchesCpuAcrossOriginalLoweredAndEmittedStages(int count, int expected)
    {
        var method = GetMethod(nameof(CountdownSum));
        Assert.Equal(expected, CountdownSum(count));
        CheckSemanticStages(method, [new Value.Integer(count)], new Value.Integer(expected));
    }

    [Theory]
    [InlineData(nameof(ReturnBool), typeof(BoolType), typeof(IntType<N32>))]
    [InlineData(nameof(ReturnByte), typeof(UIntType<N8>), typeof(IntType<N32>))]
    [InlineData(nameof(ReturnShort), typeof(IntType<N16>), typeof(IntType<N32>))]
    [InlineData(nameof(ReturnUInt), typeof(UIntType<N32>), typeof(IntType<N32>))]
    [InlineData(nameof(ReturnULong), typeof(UIntType<N64>), typeof(IntType<N64>))]
    public void ScalarReturnsConvertFromCanonicalStackType(
        string methodName,
        Type declaredType,
        Type canonicalType)
    {
        var body = CompilerTestPipeline.ShaderStack(GetMethod(methodName));
        var block = body.Blocks.Blocks.Single(candidate =>
            candidate.Body.Last.Node is Terminator.D.ReturnExpr<Label, ShaderStackOperand>);
        var conversion = Assert.IsAssignableFrom<IConversionOperation>(
            Assert.IsType<ShaderStackInstruction.Operation>(block.Body.Elements.Last().Node)
                  .Instruction.Operation);
        Assert.IsType(declaredType, conversion.ResultType);
        Assert.IsType(canonicalType, conversion.SourceType);
        Assert.IsType(declaredType,
            Assert.IsType<Terminator.D.ReturnExpr<Label, ShaderStackOperand>>(block.Body.Last.Node).Expr.Type);
    }

    [Fact]
    public void CilShaderHandoffRejectsCanonicalI32MasqueradingAsBool()
    {
        var labelled = CompilerTestPipeline.Labelled(Fixtures.BoolZeroDiamond);
        var branch = labelled.PreAnnotatedCode.Instructions.Single(item =>
            item.Node.Instruction.OpCode == OpCodes.Brtrue_S);

        var exception = Assert.Throws<ValidationException>(() =>
            CilToShaderStackPass.ValidateStack(
                labelled.Environment,
                branch.Node,
                branch.Annotation,
                [ShaderType.Bool]));

        Assert.Contains($"IL_{branch.Node.ByteOffset:X4}", exception.Message);
        Assert.Contains("expected [i32], got [bool]", exception.Message);
        Assert.Contains(labelled.Environment.Method.Name, exception.Message);
    }

    [Fact]
    public void ShaderStackConstructionRejectsInvalidAliasesOperandsTransitionsAndEdges()
    {
        var shader = CompilerTestPipeline.ShaderStack(Fixtures.BoolZeroDiamond);
        var parameter = shader.Declaration.Parameters[0].Value;
        _ = new ShaderStackInstruction.PushAlias(ShaderStackOperand.Resolved(parameter));
        Assert.Throws<ArgumentException>(() =>
            ShaderStackOperand.Resolved(ShaderValue.Intermediate(ShaderType.I32)));
        Assert.Throws<ArgumentException>(() =>
            new ShaderStackInstruction.PushAlias(
                ShaderStackOperand.Resolved(ShaderValue.Literal(new I32Literal(1)))));
        Assert.Throws<ArgumentException>(() =>
            new ShaderStackInstruction.PushAlias(ShaderStackOperand.Resolved(shader.Declaration)));

        var conversion = ScalarConversionOperation<IntType<N32>, BoolType>.Instance;
        ShaderStackInstruction Operation(ShaderStackOperand operand, int popCount) =>
            new ShaderStackInstruction.Operation(
                Instruction<ShaderStackOperand, IShaderType>.Create(
                    conversion,
                    ShaderType.Bool,
                    [operand]),
                popCount);
        Assert.Contains("exceeds stack size", Assert.Throws<ArgumentException>(() =>
            ShaderStackValidation.Apply(Operation(ShaderStackOperand.At(0, ShaderType.I32), 0), [])).Message);
        Assert.Contains("claims i32", Assert.Throws<ArgumentException>(() =>
            ShaderStackValidation.Apply(
                Operation(ShaderStackOperand.At(0, ShaderType.I32), 1),
                [ShaderType.Bool])).Message);
        Assert.Contains("Invalid typed shader-stack operation", Assert.Throws<ArgumentException>(() =>
            ShaderStackValidation.Apply(
                Operation(ShaderStackOperand.At(0, ShaderType.Bool), 1),
                [ShaderType.Bool])).Message);
        Assert.Contains("pops beyond", Assert.Throws<ArgumentException>(() =>
            ShaderStackValidation.Apply(
                new ShaderStackInstruction.Operation(
                    Instruction<ShaderStackOperand, IShaderType>.Create(NopOperation.Instance, null, []),
                    1),
                [])).Message);

        var source = shader.Source.RawCode.Instructions[0];
        var provenance = ShaderStackProvenance.Source(source, 0);
        var label = Label.Create("malformed");
        var literal = new ShaderStackInstruction.Operation(
            Instruction<ShaderStackOperand, IShaderType>.Create(
                new LiteralOperation(),
                ShaderType.I32,
                [ShaderStackOperand.Resolved(ShaderValue.Literal(new I32Literal(1)))]),
            0);
        var returned = Terminator.B.ReturnExpr<Label, ShaderStackOperand>(
            ShaderStackOperand.At(0, ShaderType.I32));
        Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition> Return() =>
            new(
                returned,
                new ShaderStackTransition([ShaderType.I32], [], provenance),
                CilStagePrettyPrinter.PrintShaderStackTerminator);
        Assert.Contains("Pre does not match", Assert.Throws<ArgumentException>(() =>
            new ShaderStackBasicBlock(
                label,
                [],
                Seq.Create([
                    new Annotated<ShaderStackInstruction, ShaderStackTransition>(
                        literal,
                        new ShaderStackTransition([ShaderType.Bool], [ShaderType.I32], provenance),
                        CilStagePrettyPrinter.PrintShaderStackInstruction)
                ], Return()))).Message);
        Assert.Contains("Post does not match", Assert.Throws<ArgumentException>(() =>
            new ShaderStackBasicBlock(
                label,
                [],
                Seq.Create([
                    new Annotated<ShaderStackInstruction, ShaderStackTransition>(
                        literal,
                        new ShaderStackTransition([], [ShaderType.Bool], provenance),
                        CilStagePrettyPrinter.PrintShaderStackInstruction)
                ], Return()))).Message);

        var target = shader.Blocks.Blocks.Single(block =>
            block.EntryStack.SequenceEqual([ShaderType.I32]));
        var sourceBlock = shader.Blocks.Blocks.First(block =>
            block.EntryStack.IsEmpty &&
            block.Successor.AllTargets().Any(candidate => ReferenceEquals(candidate, target.Label)));
        var boolLiteral = new ShaderStackInstruction.Operation(
            Instruction<ShaderStackOperand, IShaderType>.Create(
                new LiteralOperation(),
                ShaderType.Bool,
                [ShaderStackOperand.Resolved(ShaderValue.Literal(new BoolLiteral(true)))]),
            0);
        var mismatched = new ShaderStackBasicBlock(
            sourceBlock.Label,
            sourceBlock.EntryStack,
            Seq.Create([
                new Annotated<ShaderStackInstruction, ShaderStackTransition>(
                    boolLiteral,
                    new ShaderStackTransition([], [ShaderType.Bool], provenance),
                    CilStagePrettyPrinter.PrintShaderStackInstruction)
            ], new Annotated<ITerminator<Label, ShaderStackOperand>, ShaderStackTransition>(
                Terminator.B.Br<Label, ShaderStackOperand>(target.Label),
                new ShaderStackTransition([ShaderType.Bool], [ShaderType.Bool], provenance),
                CilStagePrettyPrinter.PrintShaderStackTerminator)));
        var blocks = new BlockList<ShaderStackBasicBlock>(
            shader.Blocks.EntryLabel,
            shader.Blocks.Blocks.Select(block =>
                    ReferenceEquals(block.Label, sourceBlock.Label) ? mismatched : block)
                .ToImmutableArray(),
            static block => block.Successor,
            CilStagePrettyPrinter.PrintShaderStackBlockList);
        var forged = new ShaderStackFunctionBody(shader.Source, blocks);
        var graph = ControlFlowGraph.Create(
            forged.Blocks,
            static block => block.Successor,
            CilStagePrettyPrinter.PrintShaderStackGraph);
        Assert.Contains("mismatched stack types", Assert.Throws<ArgumentException>(() =>
            new ShaderStackControlFlowBody(forged, graph)).Message);
    }

    private void CheckSemanticStages(
        MethodInfo method,
        ImmutableArray<Value> arguments,
        Value expected)
    {
        var stages = CompilerTestPipeline.CompileStages(method);
        var original = Body(stages.Compiled, method);
        var labelled = Body(stages.Labelled, method);
        Assert.True(labelled.Labels.ToHashSet().SetEquals(original.Labels));
        var lowered = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(original));
        var source = ScalarControlFlowTests.Emit(lowered);
        var originalExecution = RunCfg(original, arguments);
        Assert.Equal(expected, originalExecution.Result);
        AssertEquivalent(originalExecution, RunCfg(lowered, arguments));
        AssertEquivalent(originalExecution, new EmittedScalarProgram(lowered, source).Run(arguments));
        output.WriteLine(
            $"verified configuration={GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration}; " +
            $"method={method.Module.ModuleVersionId}:{method.MetadataToken}:{method}; result={originalExecution.Result}; " +
            $"trace={string.Join(" -> ", originalExecution.Trace)}");
        output.WriteLine(source);
    }

    private static void AssertValue(
        MethodInfo method,
        ImmutableArray<Value> arguments,
        Value expected)
    {
        var actual = RunValueCfg(CompilerTestPipeline.ValueControlFlow(method), arguments);
        Assert.Equal(expected, actual.Result);
    }

    private static string Emit(MethodInfo method)
    {
        var module = CilModuleCompiler.Compile(CompilerTestPipeline.ParseRaw(method))
                                      .RunPass(new FunctionToOperationPass())
                                      .RunPass(new StablePointerRegionParameterPass());
        return new SlangEmitter(new SlangTargetLowering().Lower(module)).Emit();
    }

    private static bool Unordered(double left, double right) =>
        double.IsNaN(left) || double.IsNaN(right);

    private static ImmutableArray<ShaderStackInstruction.Operation> Operations(ShaderStackFunctionBody body) =>
        [.. body.Blocks.Blocks.SelectMany(block => block.Body.Elements)
               .Select(item => item.Node)
               .OfType<ShaderStackInstruction.Operation>()];

    private static void AssertConversion<TSource, TTarget>(
        Annotated<ShaderStackInstruction, ShaderStackTransition> instruction)
        where TSource : IScalarType<TSource>
        where TTarget : IScalarType<TTarget> =>
        Assert.IsType<ScalarConversionOperation<TSource, TTarget>>(
            Assert.IsType<ShaderStackInstruction.Operation>(instruction.Node).Instruction.Operation);

    private static TBody Body<TBody>(
        ShaderModuleDeclaration<TBody> module,
        MethodBase method)
        where TBody : DualDrill.CLSL.Language.FunctionBody.IFunctionBody =>
        Assert.Single(module.FunctionDefinitions,
            pair => pair.Key.Name == method.Name).Value;

    private static MethodInfo GetMethod(string name) =>
        typeof(ShaderStackPipelineTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static int BoolInt(bool flag, int value) => flag ? value + 5 : value;
    private static void Observe(int value)
    {
    }
    private static int CallBoolAndObserve(bool flag, int value)
    {
        var result = BoolInt(flag, value);
        Observe(result);
        return result;
    }

    private struct FlagCell
    {
        public bool Flag;
    }

    private static bool StoreFlag(FlagCell cell, bool flag)
    {
        cell.Flag = flag;
        return flag;
    }

    private static bool ReturnBool(bool value) => value;
    private static byte ReturnByte(byte value) => value;
    private static short ReturnShort(short value) => value;
    private static uint ReturnUInt(uint value) => value;
    private static ulong ReturnULong(ulong value) => value;

    private static int Compound(int left, int right, bool gate) =>
        (left > right) == gate ? 11 : 29;

    private static int CountdownSum(int count)
    {
        var sum = 0;
        while (count > 0)
        {
            sum += count;
            count--;
        }
        return sum;
    }

    private static class Fixtures
    {
        private static readonly Type Type = Build();
        public static MethodInfo BoolZeroDiamond => Method(nameof(BoolZeroDiamond));
        public static MethodInfo UnsignedDivide => Method(nameof(UnsignedDivide));
        public static MethodInfo UnsignedRemainder => Method(nameof(UnsignedRemainder));
        public static MethodInfo UnorderedLessThan => Method(nameof(UnorderedLessThan));
        public static MethodInfo UnorderedNotEqual => Method(nameof(UnorderedNotEqual));
        public static MethodInfo UnorderedGreaterF64 => Method(nameof(UnorderedGreaterF64));
        public static MethodInfo UnorderedGreaterOrEqualF64 => Method(nameof(UnorderedGreaterOrEqualF64));
        public static MethodInfo UnorderedLessF64 => Method(nameof(UnorderedLessF64));
        public static MethodInfo UnorderedLessOrEqualF64 => Method(nameof(UnorderedLessOrEqualF64));
        public static MethodInfo UnorderedLessF32 => Method(nameof(UnorderedLessF32));
        public static MethodInfo UnorderedGreaterExpressionF32 => Method(nameof(UnorderedGreaterExpressionF32));
        public static MethodInfo NativeGreater => Method(nameof(NativeGreater));
        public static MethodInfo UnsignedGreater => Method(nameof(UnsignedGreater));
        public static MethodInfo UnsignedGreaterOrEqual => Method(nameof(UnsignedGreaterOrEqual));
        public static MethodInfo UnsignedLess => Method(nameof(UnsignedLess));
        public static MethodInfo UnsignedLessOrEqual => Method(nameof(UnsignedLessOrEqual));

        private static MethodInfo Method(string name) =>
            Type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} fixture was not found.");

        private static Type Build()
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("ShaderStackPipelineFixtures"),
                AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("ShaderStackPipelineFixtures").DefineType(
                "ShaderStackPipelineFixtures",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var diamond = Define(type, nameof(BoolZeroDiamond), typeof(int), ("flag", typeof(bool)));
            var diamondIl = diamond.GetILGenerator();
            var right = diamondIl.DefineLabel();
            var join = diamondIl.DefineLabel();
            var yes = diamondIl.DefineLabel();
            diamondIl.Emit(OpCodes.Ldarg_0);
            diamondIl.Emit(OpCodes.Brfalse_S, right);
            diamondIl.Emit(OpCodes.Ldarg_0);
            diamondIl.Emit(OpCodes.Br_S, join);
            diamondIl.MarkLabel(right);
            diamondIl.Emit(OpCodes.Ldc_I4_0);
            diamondIl.MarkLabel(join);
            diamondIl.Emit(OpCodes.Brtrue_S, yes);
            diamondIl.Emit(OpCodes.Ldc_I4, 29);
            diamondIl.Emit(OpCodes.Ret);
            diamondIl.MarkLabel(yes);
            diamondIl.Emit(OpCodes.Ldc_I4, 11);
            diamondIl.Emit(OpCodes.Ret);

            var unsignedDivide = Define(
                type,
                nameof(UnsignedDivide),
                typeof(uint),
                ("left", typeof(uint)),
                ("right", typeof(uint)));
            var unsignedIl = unsignedDivide.GetILGenerator();
            unsignedIl.Emit(OpCodes.Ldarg_0);
            unsignedIl.Emit(OpCodes.Ldarg_1);
            unsignedIl.Emit(OpCodes.Div_Un);
            unsignedIl.Emit(OpCodes.Ret);

            var unsignedRemainder = Define(
                type,
                nameof(UnsignedRemainder),
                typeof(uint),
                ("left", typeof(uint)),
                ("right", typeof(uint)));
            var unsignedRemainderIl = unsignedRemainder.GetILGenerator();
            unsignedRemainderIl.Emit(OpCodes.Ldarg_0);
            unsignedRemainderIl.Emit(OpCodes.Ldarg_1);
            unsignedRemainderIl.Emit(OpCodes.Rem_Un);
            unsignedRemainderIl.Emit(OpCodes.Ret);

            var unordered = Define(
                type,
                nameof(UnorderedLessThan),
                typeof(int),
                ("left", typeof(double)),
                ("right", typeof(double)));
            var unorderedIl = unordered.GetILGenerator();
            unorderedIl.Emit(OpCodes.Ldarg_0);
            unorderedIl.Emit(OpCodes.Ldarg_1);
            unorderedIl.Emit(OpCodes.Clt_Un);
            unorderedIl.Emit(OpCodes.Ret);

            var notEqual = Define(
                type,
                nameof(UnorderedNotEqual),
                typeof(int),
                ("left", typeof(double)),
                ("right", typeof(double)));
            var notEqualIl = notEqual.GetILGenerator();
            var unequal = notEqualIl.DefineLabel();
            notEqualIl.Emit(OpCodes.Ldarg_0);
            notEqualIl.Emit(OpCodes.Ldarg_1);
            notEqualIl.Emit(OpCodes.Bne_Un_S, unequal);
            notEqualIl.Emit(OpCodes.Ldc_I4_0);
            notEqualIl.Emit(OpCodes.Ret);
            notEqualIl.MarkLabel(unequal);
            notEqualIl.Emit(OpCodes.Ldc_I4_1);
            notEqualIl.Emit(OpCodes.Ret);

            DefineBranch(type, nameof(UnorderedGreaterF64), typeof(double), OpCodes.Bgt_Un_S);
            DefineBranch(type, nameof(UnorderedGreaterOrEqualF64), typeof(double), OpCodes.Bge_Un_S);
            DefineBranch(type, nameof(UnorderedLessF64), typeof(double), OpCodes.Blt_Un_S);
            DefineBranch(type, nameof(UnorderedLessOrEqualF64), typeof(double), OpCodes.Ble_Un_S);
            DefineBranch(type, nameof(UnorderedLessF32), typeof(float), OpCodes.Blt_Un_S);
            DefineRelation(type, nameof(UnorderedGreaterExpressionF32), typeof(float), OpCodes.Cgt_Un);

            var nativeGreater = Define(
                type,
                nameof(NativeGreater),
                typeof(int),
                ("left", typeof(int)),
                ("right", typeof(int)));
            var nativeGreaterIl = nativeGreater.GetILGenerator();
            var greater = nativeGreaterIl.DefineLabel();
            nativeGreaterIl.Emit(OpCodes.Ldarg_0);
            nativeGreaterIl.Emit(OpCodes.Ldarg_1);
            nativeGreaterIl.Emit(OpCodes.Bgt_S, greater);
            nativeGreaterIl.Emit(OpCodes.Ldc_I4_0);
            nativeGreaterIl.Emit(OpCodes.Ret);
            nativeGreaterIl.MarkLabel(greater);
            nativeGreaterIl.Emit(OpCodes.Ldc_I4_1);
            nativeGreaterIl.Emit(OpCodes.Ret);

            var unsignedGreater = Define(
                type,
                nameof(UnsignedGreater),
                typeof(int),
                ("left", typeof(uint)),
                ("right", typeof(uint)));
            var unsignedGreaterIl = unsignedGreater.GetILGenerator();
            var unsignedGreaterArm = unsignedGreaterIl.DefineLabel();
            unsignedGreaterIl.Emit(OpCodes.Ldarg_0);
            unsignedGreaterIl.Emit(OpCodes.Ldarg_1);
            unsignedGreaterIl.Emit(OpCodes.Bgt_Un_S, unsignedGreaterArm);
            unsignedGreaterIl.Emit(OpCodes.Ldc_I4_0);
            unsignedGreaterIl.Emit(OpCodes.Ret);
            unsignedGreaterIl.MarkLabel(unsignedGreaterArm);
            unsignedGreaterIl.Emit(OpCodes.Ldc_I4_1);
            unsignedGreaterIl.Emit(OpCodes.Ret);

            DefineBranch(type, nameof(UnsignedGreaterOrEqual), typeof(uint), OpCodes.Bge_Un_S);
            DefineBranch(type, nameof(UnsignedLess), typeof(uint), OpCodes.Blt_Un_S);
            DefineBranch(type, nameof(UnsignedLessOrEqual), typeof(uint), OpCodes.Ble_Un_S);

            return type.CreateType()
                   ?? throw new InvalidOperationException("Failed to create shader-stack fixtures.");
        }

        private static void DefineBranch(TypeBuilder type, string name, Type operand, OpCode opCode)
        {
            var method = Define(type, name, typeof(int), ("left", operand), ("right", operand));
            var il = method.GetILGenerator();
            var matched = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(opCode, matched);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            il.MarkLabel(matched);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
        }

        private static void DefineRelation(TypeBuilder type, string name, Type operand, OpCode opCode)
        {
            var method = Define(type, name, typeof(int), ("left", operand), ("right", operand));
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(opCode);
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

    internal sealed class UnitCallShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int Fragment([Location(0)] int value)
        {
            Observe(value);
            return value;
        }

        public static void Observe(int value)
        {
        }
    }
}
