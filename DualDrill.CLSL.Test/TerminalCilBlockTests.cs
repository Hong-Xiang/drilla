using System.Reflection;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace DualDrill.CLSL.Test;

public sealed class TerminalCilBlockTests
{
    [Fact]
    public void ConditionalReturnPreservesConfigurationSpecificTerminalCil()
    {
        Assert.Equal(29, ConditionalReturn(11, false, 29));
        Assert.Equal(11, ConditionalReturn(11, true, 29));

        var method = typeof(TerminalCilBlockTests).GetMethod(
            nameof(ConditionalReturn),
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Conditional return fixture was not found.");
        var stages = CompilerTestPipeline.CompileStages(method);
        var raw = CompilerTestPipeline.RawBody(stages.Raw, method);
        var declaration = raw.Declaration;
        var parsed = Assert.Single(
            stages.Compiled.FunctionDefinitions.Values,
            body => ReferenceEquals(body.Declaration, declaration));
        var model = Assert.Single(
            stages.ControlFlow.FunctionDefinitions.Values,
            body => body.Environment.Method == method);
        var graph = model.ControlFlow;
        var blocks = model.Labels.ToDictionary(
            label => graph[label].ByteOffset,
            label => graph[label]);

        Assert.True(model.Labels.ToHashSet().SetEquals(parsed.Labels));

        switch (typeof(TerminalCilBlockTests).Assembly
                    .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration)
        {
            case "Debug":
                Assert.Equal(8, model.CodeByteSize);
                AssertRanges(blocks,
                    (0, 0, 2, 3),
                    (3, 2, 2, 3),
                    (6, 4, 1, 1),
                    (7, 5, 1, 1));
                Assert.Equal(blocks[7].Label,
                    Assert.IsType<UnconditionalSuccessor>(
                        graph.Successor(blocks[6].Label)).Target);
                AssertTerminalRet(model, blocks[7]);
                AssertDebugValueFlow(parsed, declaration, blocks);
                break;
            case "Release":
                Assert.Equal(7, model.CodeByteSize);
                AssertRanges(blocks,
                    (0, 0, 2, 3),
                    (3, 2, 2, 2),
                    (5, 4, 2, 2));
                AssertTerminalRet(model, blocks[3]);
                AssertTerminalRet(model, blocks[5]);
                AssertReleaseValueFlow(parsed, declaration, blocks);
                break;
            default:
                throw new InvalidOperationException("Unsupported build configuration.");
        }
    }

    private static int ConditionalReturn(int left, bool choose, int right) => choose ? left : right;

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

    private static void AssertTerminalRet(
        MethodBodyAnalysisModel model,
        CilInstructionBlock block)
    {
        Assert.IsType<TerminateSuccessor>(model.ControlFlow.Successor(block.Label));
        var control = Assert.IsType<CilControlFlow.Return>(block.Terminator);
        var last = block.Instructions[^1].Node;
        Assert.Equal(OpCodes.Ret, last.Instruction.OpCode);
        Assert.Equal(last, control.Instruction);
        Assert.Same(last.Instruction, control.Instruction.Instruction);
    }

    private static void AssertDebugValueFlow(
        FunctionBody4 body,
        FunctionDeclaration declaration,
        IReadOnlyDictionary<int, CilInstructionBlock> blocks)
    {
        var branch = Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(
            body[blocks[0].Label].Body.Last);
        Assert.Equal(blocks[6].Label, branch.TrueTarget.Label);
        Assert.Equal(blocks[3].Label, branch.FalseTarget.Label);

        AssertArmJumpsTo(body, blocks[6].Label, declaration.Parameters[0], blocks[7].Label);
        AssertArmJumpsTo(body, blocks[3].Label, declaration.Parameters[2], blocks[7].Label);

        var terminal = body[blocks[7].Label];
        var parameter = Assert.Single(terminal.Parameters);
        Assert.Empty(terminal.Body.Elements);
        Assert.Same(parameter,
            Assert.IsType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>(
                terminal.Body.Last).Expr);
    }

    private static void AssertReleaseValueFlow(
        FunctionBody4 body,
        FunctionDeclaration declaration,
        IReadOnlyDictionary<int, CilInstructionBlock> blocks)
    {
        var branch = Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(
            body[blocks[0].Label].Body.Last);
        Assert.Equal(blocks[5].Label, branch.TrueTarget.Label);
        Assert.Equal(blocks[3].Label, branch.FalseTarget.Label);

        AssertArmReturns(body, blocks[5].Label, declaration.Parameters[0]);
        AssertArmReturns(body, blocks[3].Label, declaration.Parameters[2]);
    }

    private static void AssertArmJumpsTo(
        FunctionBody4 body,
        Label arm,
        ParameterDeclaration parameter,
        Label terminal)
    {
        var load = Assert.Single(body[arm].Body.Elements);
        Assert.IsType<LoadOperation>(load.Operation);
        Assert.Same(parameter.Value, load.Operand0);

        var jump =
            Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(body[arm].Body.Last).Target;
        Assert.Equal(terminal, jump.Label);
        Assert.Same(load.Result, Assert.Single(jump.Arguments));
    }

    private static void AssertArmReturns(
        FunctionBody4 body,
        Label arm,
        ParameterDeclaration parameter)
    {
        var load = Assert.Single(body[arm].Body.Elements);
        Assert.IsType<LoadOperation>(load.Operation);
        Assert.Same(parameter.Value, load.Operand0);
        Assert.Same(load.Result,
            Assert.IsType<Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>>(
                body[arm].Body.Last).Expr);
    }
}
