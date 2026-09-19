using System.Reflection;
using System.Reflection.Metadata;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Test;

public sealed class LinearCilControlFlowTests
{
    [Fact]
    public void LinearInstructionsRemainOrderedThroughConcreteBlocks()
    {
        var model = CreateModel(nameof(CountDown));
        var graph = model.ControlFlow;
        var blocks = model.Labels.Select(label => graph[label]).OrderBy(block => block.InstructionIndex);
        var blockedInstructions = blocks.SelectMany(block => block.Instructions).Select(item => item.Node).ToArray();

        Assert.Equal(model.RawCode.Instructions.Length, blockedInstructions.Length);
        Assert.Equal(model.RawCode.Instructions, blockedInstructions);
        Assert.Equal(Enumerable.Range(0, model.InstructionCount),
            model.RawCode.Instructions.Select(instruction => instruction.Index));
        Assert.Equal(
            model.Environment.Offsets.SkipLast(1),
            model.RawCode.Instructions.Select(instruction => instruction.ByteOffset));
        Assert.Equal(
            model.Environment.Offsets.Skip(1),
            model.RawCode.Instructions.Select(instruction => instruction.NextByteOffset));
    }

    [Fact]
    public void ConcreteControlPreservesForwardBackwardAndFallthroughTopology()
    {
        var model = CreateModel(nameof(CountDown));
        var graph = model.ControlFlow;
        var blocks = model.Labels.Select(label => graph[label]).ToArray();

        Assert.Contains(blocks,
            block => Targets(block.Terminator).Any(target => model.LabelToInstructionIndex(target) >
                                                             block.InstructionIndex));
        Assert.Contains(blocks,
            block => Targets(block.Terminator).Any(target => model.LabelToInstructionIndex(target) <
                                                             block.InstructionIndex));
        Assert.Contains(blocks, block => block.Terminator is CilControlFlow.FallThrough);

        foreach (var block in blocks)
            Assert.Equal(
                block.Terminator.ToSuccessor().AllTargets(),
                graph.Successor(block.Label).AllTargets());
    }

    [Fact]
    public void NativeProjectionAndLoweredBooleanKeepDistinctArmMeanings()
    {
        var method = GetMethod(nameof(Choose));
        var stages = CompilerTestPipeline.CompileStages(method);
        var model = Assert.Single(
            stages.ControlFlow.FunctionDefinitions.Values,
            body => body.Environment.Method == method);
        var graph = model.ControlFlow;
        var control = model.Labels.Select(label => graph[label].Terminator)
                           .OfType<CilControlFlow.ConditionalBranch>()
                           .Single();
        var successor = Assert.IsType<ConditionalSuccessor>(control.ToSuccessor());
        var block = model.Labels.Single(label => ReferenceEquals(graph[label].Terminator, control));
        var valueBody = Assert.Single(
            stages.ValueControlFlow.FunctionDefinitions.Values,
            body => body.Source.Environment.Method == method);
        var lowered = Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(
            valueBody.Graph[block].Body.Last);

        Assert.Equal(control.BranchTarget, successor.TrueTarget);
        Assert.Equal(control.FallThroughTarget, successor.FalseTarget);

        switch (control.Instruction.Instruction.OpCode.ToILOpCode())
        {
            case ILOpCode.Brfalse:
            case ILOpCode.Brfalse_s:
                Assert.Equal(control.FallThroughTarget, lowered.TrueTarget.Label);
                Assert.Equal(control.BranchTarget, lowered.FalseTarget.Label);
                break;
            case ILOpCode.Brtrue:
            case ILOpCode.Brtrue_s:
                Assert.Equal(control.BranchTarget, lowered.TrueTarget.Label);
                Assert.Equal(control.FallThroughTarget, lowered.FalseTarget.Label);
                break;
            default:
                throw new InvalidOperationException(
                    $"Expected brtrue/brfalse, got {control.Instruction.Instruction.OpCode}.");
        }
    }

    [Fact]
    public void ExceptionHandlingAndItsControlOpcodesAreRejected()
    {
        var method = GetMethod(nameof(TryFinally));
        var raw = CilMethodDecoder.Decode(method);
        var instructions = raw.Instructions;
        var exception = Assert.Throws<NotSupportedException>(() =>
            CilPreStackPass.Run(CompilerTestPipeline.ParseRaw(method)));
        var leave = Assert.Single(instructions,
            instruction => instruction.Instruction.OpCode.ToILOpCode() is ILOpCode.Leave or ILOpCode.Leave_s);
        var endFinally = Assert.Single(instructions,
            instruction => instruction.Instruction.OpCode.ToILOpCode() == ILOpCode.Endfinally);

        Assert.Contains(method.Name, exception.Message);
        Assert.Throws<ArgumentException>(() => new CilControlFlow.Branch(leave, Label.Create(0)));
        Assert.Throws<ArgumentException>(() => new CilControlFlow.Return(endFinally));
    }

    [Fact]
    public void ConditionalProjectionPreservesParallelArmsAndConcretePayload()
    {
        var model = CreateModel(nameof(Choose));
        var modelGraph = model.ControlFlow;
        var originalBlock = model.Labels.Select(label => modelGraph[label])
                                 .Single(block => block.Terminator is CilControlFlow.ConditionalBranch);
        var original = Assert.IsType<CilControlFlow.ConditionalBranch>(originalBlock.Terminator);
        var sameTarget = new CilControlFlow.ConditionalBranch(
            original.Instruction,
            original.BranchTarget,
            original.BranchTarget);
        var successor = Assert.IsType<ConditionalSuccessor>(sameTarget.ToSuccessor());
        var projectedGraph = new ControlFlowGraph<CilControlFlow>(
            original.BranchTarget,
            new Dictionary<Label, ControlFlowGraph<CilControlFlow>.NodeDefinition>
            {
                [original.BranchTarget] = new(sameTarget.ToSuccessor(), sameTarget)
            });

        Assert.Equal([original.BranchTarget, original.BranchTarget], successor.GetReferencedLabels());
        Assert.Single(projectedGraph.Predecessor(original.BranchTarget));
        Assert.Same(sameTarget, projectedGraph[original.BranchTarget]);

        Assert.Same(originalBlock, modelGraph[originalBlock.Label]);
        Assert.Same(original.Instruction.Instruction,
            Assert.IsType<CilControlFlow.ConditionalBranch>(
                modelGraph[originalBlock.Label].Terminator).Instruction.Instruction);
    }

    private static MethodBodyAnalysisModel CreateModel(string name)
        => CompilerTestPipeline.ControlFlow(GetMethod(name));

    private static MethodInfo GetMethod(string name)
    {
        var method = typeof(LinearCilControlFlowTests).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{name} fixture was not found.");
        return method;
    }

    private static CilInstructionInfo[] Decode(MethodInfo method)
    {
        var instructions = method.GetInstructions()?.ToArray()
                           ?? throw new InvalidOperationException($"{method} has no CIL body.");
        var codeSize = method.GetMethodBody()?.GetILAsByteArray()?.Length
                       ?? throw new InvalidOperationException($"{method} has no CIL bytes.");
        return
        [
            .. instructions.Select((instruction, index) => new CilInstructionInfo(
                index,
                instruction.Offset,
                index + 1 < instructions.Length ? instructions[index + 1].Offset : codeSize,
                instruction))
        ];
    }

    private static IEnumerable<Label> Targets(CilControlFlow control) =>
        control.ToSuccessor().AllTargets();

    private static int CountDown(int value)
    {
        while (value > 0)
            value--;
        return value;
    }

    private static int Choose(bool choose, int left, int right) => choose ? left : right;

    private static int TryFinally(int value)
    {
        try
        {
            value++;
        }
        finally
        {
            value--;
        }

        return value;
    }
}
