using System.Reflection;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class IntegerInequalityBranchTests
{
    [Fact]
    public void I32EqualityBranchPreservesActualOperandTypesAndArms()
    {
        AssertTypedBranch<IntType<N32>>(GetMethod(nameof(IntegerInequalityShader.SelectEqual32)));
    }

    [Fact]
    public void I64EqualityBranchPreservesActualOperandTypesAndArms()
    {
        AssertTypedBranch<IntType<N64>>(GetMethod(nameof(IntegerInequalityShader.SelectEqual64)));
    }

    [Fact]
    public void EqualityFixturesSelectExpectedArmsAtSignedBoundaries()
    {
        Assert.Equal(11, IntegerInequalityShader.SelectEqual32(-1, -1));
        Assert.Equal(29, IntegerInequalityShader.SelectEqual32(-1, 0));
        Assert.Equal(11, IntegerInequalityShader.SelectEqual32(int.MinValue, int.MinValue));
        Assert.Equal(29, IntegerInequalityShader.SelectEqual32(int.MinValue, int.MaxValue));

        Assert.Equal(11, IntegerInequalityShader.SelectEqual64(-1, -1));
        Assert.Equal(29, IntegerInequalityShader.SelectEqual64(-1, 0));
        Assert.Equal(11, IntegerInequalityShader.SelectEqual64(long.MinValue, long.MinValue));
        Assert.Equal(29, IntegerInequalityShader.SelectEqual64(long.MinValue, long.MaxValue));
    }

    [Fact]
    public void I32EqualityBranchCompilesThroughPublicSlangAndWgslApis()
    {
        var shader = new IntegerInequalityShader();
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);

        Assert.Contains(
#if DEBUG
            "==",
#else
            "!=",
#endif
            slang);
        Assert.Contains("@fragment", wgsl);
        Assert.Contains("fn fs", wgsl);
    }

    private static MethodInfo GetMethod(string name) =>
        typeof(IntegerInequalityShader).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{name} fixture was not found.");

    private static void AssertTypedBranch<TInteger>(MethodInfo method)
        where TInteger : INumericType<TInteger>
    {
        var stages = CompilerTestPipeline.CompileStages(method);
        var declaration = CompilerTestPipeline.RawBody(stages.Raw, method).Declaration;
        var body = Assert.Single(
            stages.Compiled.FunctionDefinitions.Values,
            value => ReferenceEquals(value.Declaration, declaration));
        var model = Assert.Single(
            stages.Labelled.FunctionDefinitions.Values,
            value => value.Environment.Method == method);
        var blocks = model.Blocks.Blocks.ToDictionary(block => block.ByteOffset);

        Assert.True(model.Labels.ToHashSet().SetEquals(body.Labels));

        var configuration = typeof(IntegerInequalityBranchTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        switch (configuration)
        {
            case "Debug":
                Assert.Equal(21, model.CodeByteSize);
                AssertRanges(blocks,
                    (0, 0, 7, 9),
                    (9, 7, 3, 5),
                    (14, 10, 3, 5),
                    (19, 13, 2, 2));
                Assert.Equal(OpCodes.Ceq, model[model.Environment.OffsetsToIndex[3]].Instruction.OpCode);
                AssertRelationalOperation<TInteger, BinaryRelational.Eq>(body[blocks[0].Label]);
                AssertArms(body, blocks[0].Label, blocks[9].Label, blocks[14].Label);
                AssertArmLiteral(body[blocks[9].Label], 11);
                AssertArmLiteral(body[blocks[14].Label], 29);
                break;
            case "Release":
                Assert.Equal(10, model.CodeByteSize);
                AssertRanges(blocks,
                    (0, 0, 3, 4),
                    (4, 3, 2, 3),
                    (7, 5, 2, 3));
                Assert.Equal(OpCodes.Bne_Un_S, model[model.Environment.OffsetsToIndex[2]].Instruction.OpCode);
                var nativeBranch =
                    Assert.IsType<CilControlFlow.ConditionalBranch>(blocks[0].Terminator);
                Assert.Equal(OpCodes.Bne_Un_S, nativeBranch.Instruction.Instruction.OpCode);
                Assert.Same(model[model.Environment.OffsetsToIndex[2]].Instruction,
                    nativeBranch.Instruction.Instruction);
                AssertRelationalOperation<TInteger, BinaryRelational.Ne>(body[blocks[0].Label]);
                AssertArms(body, blocks[0].Label, blocks[7].Label, blocks[4].Label);
                AssertReturningLiteral(body[blocks[4].Label], 11);
                AssertReturningLiteral(body[blocks[7].Label], 29);
                break;
            default:
                throw new InvalidOperationException($"Unsupported build configuration {configuration}.");
        }
    }

    private static void AssertRanges(
        IReadOnlyDictionary<int, CilInstructionBlock> blocks,
        params (int Offset, int Index, int Count, int Length)[] expected)
    {
        Assert.Equal(expected.Select(item => item.Offset), blocks.Keys.Order());
        foreach (var (offset, index, count, length) in expected)
        {
            var block = blocks[offset];
            Assert.Equal(index, block.InstructionIndex);
            Assert.Equal(count, block.InstructionCount);
            Assert.Equal(length, block.ByteLength);
        }
    }

    private static void AssertRelationalOperation<TInteger, TOperation>(ShaderRegionBody block)
        where TInteger : INumericType<TInteger>
        where TOperation : BinaryRelational.IOp<TOperation>
    {
        var instruction = Assert.Single(
            block.Body.Elements,
            candidate => candidate.Operation is INumericBinaryRelationalOperation);
        IBinaryExpressionOperation operation =
            Assert.IsType<NumericBinaryRelationalOperation<TInteger, TOperation>>(instruction.Operation);

        Assert.Equal(instruction.Operand0?.Type, operation.LeftType);
        Assert.Equal(instruction.Operand1?.Type, operation.RightType);
        Assert.Equal(TInteger.Instance, operation.LeftType);
        Assert.Equal(TInteger.Instance, operation.RightType);
    }

    private static void AssertArms(RegionFunctionBody body, Label branch, Label trueTarget, Label falseTarget)
    {
        var terminator =
            Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(body[branch].Body.Last);
        Assert.Equal(trueTarget, terminator.TrueTarget.Label);
        Assert.Equal(falseTarget, terminator.FalseTarget.Label);
    }

    private static void AssertArmLiteral(ShaderRegionBody block, int expected)
    {
        var literal = Assert.Single(block.Body.Elements, instruction => instruction.Operation is LiteralOperation);
        Assert.Equal(expected, Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(literal.Operand0).Value).Value);
    }

    private static void AssertReturningLiteral(ShaderRegionBody block, int expected)
    {
        var literal = Assert.Single(block.Body.Elements);
        Assert.Equal(expected, Assert.IsType<I32Literal>(Assert.IsType<LiteralValue>(literal.Operand0).Value).Value);
        Assert.Same(literal.Result,
            Assert.IsType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>(block.Body.Last).Expr);
    }

    private sealed class IntegerInequalityShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int fs([Location(0)] int value) => SelectEqual32(value, -1);

        public static int SelectEqual32(int left, int right)
        {
            if (left == right)
                return 11;
            return 29;
        }

        public static int SelectEqual64(long left, long right)
        {
            if (left == right)
                return 11;
            return 29;
        }
    }
}
