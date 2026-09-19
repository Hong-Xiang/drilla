using System.Collections.Frozen;
using System.Collections.Immutable;
using DualDrill.CLSL.Compiler;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using Lokad.ILPack.IL;
using FlowControl = System.Reflection.Emit.FlowControl;

namespace DualDrill.CLSL.Frontend;

internal static class CilBlockPartitioner
{
    internal static BlockList<CilInstructionBlock> Partition(
        LinearCode<CilInstructionInfo> raw,
        LinearCode<Annotated<CilInstructionInfo, PreStack>> preAnnotated)
    {
        ValidateSourceAssociation(raw, preAnnotated);
        var environment = raw.Environment;
        var byIndex = preAnnotated.Instructions.ToFrozenDictionary(instruction => instruction.Node.Index);
        var reachable = byIndex.Keys.ToFrozenSet();
        var builder = new InstructionBlockPartitioner(
            raw.Count,
            index => Label.Create(environment.Offsets[index]));

        foreach (var index in reachable.Order())
        {
            var instruction = raw[index];
            var opCode = instruction.Instruction.OpCode.ToILOpCode();
            switch (instruction.Instruction.OpCode.FlowControl)
            {
                case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                    builder.AddBr(instruction.Index, environment.ResolveBranchTarget(instruction));
                    break;
                case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                    builder.AddBrIf(instruction.Index, environment.ResolveBranchTarget(instruction));
                    break;
                case FlowControl.Return when CilControlFlow.IsReturn(opCode):
                    builder.AddReturn(instruction.Index);
                    break;
                case FlowControl.Next:
                case FlowControl.Call:
                    break;
                default:
                    throw new NotSupportedException(
                        $"CIL control {opCode} at IL_{instruction.ByteOffset:X4} " +
                        $"is not supported for method {environment.Method}.");
            }
        }

        return builder.BuildReachable(
            reachable,
            (label, range, successor) =>
            {
                var instructions = Enumerable.Range(range.Start, range.Count)
                                             .Select(index => byIndex[index])
                                             .ToImmutableArray();
                var last = instructions[^1].Node;
                CilControlFlow terminator = (last.Instruction.OpCode.FlowControl, successor) switch
                {
                    (FlowControl.Branch, UnconditionalSuccessor { Target: var target }) =>
                        new CilControlFlow.Branch(last, target),
                    (FlowControl.Cond_Branch,
                        ConditionalSuccessor { TrueTarget: var branchTarget, FalseTarget: var fallThroughTarget }) =>
                        new CilControlFlow.ConditionalBranch(last, branchTarget, fallThroughTarget),
                    (FlowControl.Return, TerminateSuccessor) =>
                        new CilControlFlow.Return(last),
                    (FlowControl.Next or FlowControl.Call, UnconditionalSuccessor { Target: var target }) =>
                        new CilControlFlow.FallThrough(target),
                    (FlowControl.Next or FlowControl.Call, TerminateSuccessor) =>
                        new CilControlFlow.EndOfCode(),
                    _ => throw new InvalidProgramException(
                        $"CIL control and block boundaries disagree at IL_{last.ByteOffset:X4}.")
                };

                return new CilInstructionBlock(label, instructions, terminator);
            },
            static block => block.Terminator.ToSuccessor(),
            CilStagePrettyPrinter.PrintBlockList);
    }

    private static void ValidateSourceAssociation(
        LinearCode<CilInstructionInfo> raw,
        LinearCode<Annotated<CilInstructionInfo, PreStack>> preAnnotated)
    {
        if (!ReferenceEquals(raw.Environment, preAnnotated.Environment))
            throw new ArgumentException("Raw and Pre-annotated code must share one method environment.",
                nameof(preAnnotated));

        var previousIndex = -1;
        foreach (var annotated in preAnnotated.Instructions)
        {
            var instruction = annotated.Node;
            if (instruction.Index <= previousIndex)
                throw new ArgumentException(
                    "Pre-annotated instructions must have unique, increasing original indices.",
                    nameof(preAnnotated));
            if (instruction.Index < 0 || instruction.Index >= raw.Count)
                throw new ArgumentException(
                    $"Pre-annotated instruction index {instruction.Index} is outside the raw source.",
                    nameof(preAnnotated));

            var original = raw[instruction.Index];
            if (!original.Equals(instruction) ||
                !ReferenceEquals(original.Instruction, instruction.Instruction))
                throw new ArgumentException(
                    $"Pre-annotated instruction {instruction.Index} does not belong to the raw source.",
                    nameof(preAnnotated));

            previousIndex = instruction.Index;
        }
    }
}
