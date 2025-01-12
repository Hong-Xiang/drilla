using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace DualDrill.ILSL.Frontend;


public sealed class MethodBodyBasicBlockAnalyzer
{
    public sealed record class Result(
        ImmutableArray<Instruction> Instructions,
        ImmutableArray<BasicBlockRange> BasicBlocks
    )
    {
    }

    public record struct BasicBlockRange(
        int InstructionIndex,
        int InstructionCount,
        int ByteOffset,
        int ByteSize,
        FlowKind FlowKind,
        int? FlowTarget,
        FrozenDictionary<int, int>? SwitchTargets
    )
    {
    }

    public enum FlowKind
    {
        Fallthrough,
        UnconditionalBranch,
        ConditionalBranch,
        Switch,
        Return,
    }


    public Result Run(MethodBase method)
    {
        var instructions = method.GetInstructions().ToImmutableArray();
        if (instructions.Length == 0)
        {
            return new([], []);
        }

        var instructionSizes = new int[instructions.Length];
        var nextOffsets = new int[instructions.Length];
        var offsetsToInstructionIndex = instructions.Index().ToFrozenDictionary((data) => data.Item.Offset, data => data.Index);

        {
            var methodBodyILByteSize = method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
            foreach (var (i, inst) in instructions.Index())
            {
                nextOffsets[i] = (i + 1) < instructions.Length ? instructions[i + 1].Offset : methodBodyILByteSize;
                instructionSizes[i] = nextOffsets[i] - instructions[i].Offset;
                Debug.Assert(instructionSizes[i] > 0);
            }
        }
        var isLead = new bool[instructions.Length];
        var flowKinds = new FlowKind[instructions.Length];
        isLead[0] = true;

        foreach (var (idx, inst) in instructions.Index())
        {
            switch (inst.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    flowKinds[idx] = FlowKind.UnconditionalBranch;
                    break;
                case FlowControl.Cond_Branch:
                    if (inst.OpCode.ToILOpCode() == System.Reflection.Metadata.ILOpCode.Switch)
                    {
                        flowKinds[idx] = FlowKind.Switch;
                    }
                    else
                    {
                        flowKinds[idx] = FlowKind.ConditionalBranch;
                    }
                    break;
                case FlowControl.Return:
                    flowKinds[idx] = FlowKind.Return;
                    break;
                case FlowControl.Throw:
                    throw new NotSupportedException("throw is not supported");
                // TODO: handle switch
                default:
                    continue;
            }
            if ((idx + 1) < instructions.Length)
            {
                isLead[idx + 1] = true;
            }
        }

        var blockIndex = new int[instructions.Length];
        var blockCount = 0;
        foreach (var (idx, isLeadFlag) in isLead.Index())
        {
            if (isLeadFlag)
            {
                blockIndex[idx] = blockCount;
                blockCount++;
            }
            else
            {
                blockIndex[idx] = blockCount - 1;
            }
        }
        var basicBlocks = new BasicBlockRange[blockCount];

        foreach (var (idx, inst) in instructions.Index())
        {
            ref var block = ref basicBlocks[blockIndex[idx]];
            if (isLead[idx])
            {
                if (idx > 0)
                {
                    ref var prevBlock = ref basicBlocks[blockIndex[idx - 1]];
                    prevBlock.InstructionCount = idx - prevBlock.InstructionIndex;
                    prevBlock.ByteSize = inst.Offset - prevBlock.ByteOffset;
                }
                block.InstructionIndex = idx;
                block.ByteOffset = inst.Offset;
            }

            var flowKind = flowKinds[idx];
            if (flowKind != FlowKind.Fallthrough)
            {
                Debug.Assert(block.FlowKind == FlowKind.Fallthrough);
                block.FlowKind = flowKind;
                switch (flowKind)
                {
                    case FlowKind.UnconditionalBranch:
                    case FlowKind.ConditionalBranch:
                        int jump = OpCodes.TakesSingleByteArgument(inst.OpCode) ? (sbyte)inst.Operand : (int)inst.Operand;
                        block.FlowTarget = offsetsToInstructionIndex[nextOffsets[idx] + jump];
                        break;
                    // TODO: handle switch
                    default:
                        break;
                }
            }
        }
        ref var lastBB = ref basicBlocks[^1];
        lastBB.InstructionCount = instructions.Length - lastBB.InstructionIndex;
        lastBB.ByteSize = nextOffsets[^1] - lastBB.ByteOffset;
        return new(instructions, [.. basicBlocks]);
    }
}
