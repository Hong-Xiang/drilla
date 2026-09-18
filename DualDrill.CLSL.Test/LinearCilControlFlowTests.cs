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
        var blocks = model.Labels.Select(label => model.ControlFlowGraph[label]).OrderBy(block => block.InstructionIndex);
        var blockedInstructions = blocks.SelectMany(block => block.Instructions).ToArray();

        Assert.Equal(model.Instructions.Length, blockedInstructions.Length);
        Assert.Equal(model.Instructions, blockedInstructions);
        Assert.Equal(Enumerable.Range(0, model.InstructionCount),
            model.Instructions.Select(instruction => instruction.Index));
        Assert.Equal(model.Offsets.SkipLast(1), model.Instructions.Select(instruction => instruction.ByteOffset));
        Assert.Equal(model.Offsets.Skip(1), model.Instructions.Select(instruction => instruction.NextByteOffset));
    }

    [Fact]
    public void ConcreteControlPreservesForwardBackwardAndFallthroughTopology()
    {
        var model = CreateModel(nameof(CountDown));
        var blocks = model.Labels.Select(label => model.ControlFlowGraph[label]).ToArray();

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
                model.ControlFlowGraph.Successor(block.Label).AllTargets());
    }

    [Fact]
    public void NativeProjectionAndLoweredBooleanKeepDistinctArmMeanings()
    {
        var method = GetMethod(nameof(Choose));
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(method);
        var model = parser.Context.GetFunctionDefinition(declaration);
        var control = model.Labels.Select(label => model.ControlFlowGraph[label].Terminator)
                           .OfType<CilControlFlow.ConditionalBranch>()
                           .Single();
        var successor = Assert.IsType<ConditionalSuccessor>(control.ToSuccessor());
        var block = model.Labels.Single(label => ReferenceEquals(model.ControlFlowGraph[label].Terminator, control));
        var lowered = Assert.IsType<Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue>>(
            parser.MethodBodies[declaration][block].Body.Last);

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
        var exception = Assert.Throws<NotSupportedException>(() => new MethodBodyAnalysisModel(method));
        var instructions = Decode(method);
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
        var originalBlock = model.Labels.Select(label => model.ControlFlowGraph[label])
                                 .Single(block => block.Terminator is CilControlFlow.ConditionalBranch);
        var original = Assert.IsType<CilControlFlow.ConditionalBranch>(originalBlock.Terminator);
        var sameTarget = new CilControlFlow.ConditionalBranch(
            original.Instruction,
            original.BranchTarget,
            original.BranchTarget);
        var successor = Assert.IsType<ConditionalSuccessor>(sameTarget.ToSuccessor());
        var graph = new ControlFlowGraph<CilControlFlow>(
            original.BranchTarget,
            new Dictionary<Label, ControlFlowGraph<CilControlFlow>.NodeDefinition>
            {
                [original.BranchTarget] = new(sameTarget.ToSuccessor(), sameTarget)
            });

        Assert.Equal([original.BranchTarget, original.BranchTarget], successor.GetReferencedLabels());
        Assert.Single(graph.Predecessor(original.BranchTarget));
        Assert.Same(sameTarget, graph[original.BranchTarget]);

        _ = model.ControlFlowGraph.ControlFlowAnalysis();
        Assert.Same(originalBlock, model.ControlFlowGraph[originalBlock.Label]);
        Assert.Same(original.Instruction.Instruction,
            Assert.IsType<CilControlFlow.ConditionalBranch>(
                model.ControlFlowGraph[originalBlock.Label].Terminator).Instruction.Instruction);
    }

    private static MethodBodyAnalysisModel CreateModel(string name)
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(GetMethod(name));
        return parser.Context.GetFunctionDefinition(declaration);
    }

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
