using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace DualDrill.ILSL;

interface IInstructionInfo
{
    OpCode OpCode { get; }
    ReadOnlySpan<Type> OpStackBefore { get; }
    ReadOnlySpan<Type> OpStackAfter { get; }
    int Offset { get; }
    int Size { get; }
    int NextOffset { get; }
    bool IsLead { get; }
    object Operand { get; }
    ILOpCode ILOpCode { get; }
}

enum FlowKind
{
    Next,
    UnconditionalBranch,
    ConditonalFalseBranch,
    ConditionalTrueBranch,
    SwitchDefaultBranch,
    SwitchCaseBranch,
    Return,
}

readonly record struct FlowEdge(
    int SourceBasicBlockIndex,
    int TargetBasicBlockIndex,
    FlowKind Kind
)
{
}

interface IBasicBlockInfo
{
    int BlockIndex { get; }
    int ByteOffset { get; }
    int ByteCount { get; }
    int InstructionOffset { get; }
    int InstructionCount { get; }
    ReadOnlySpan<Type> OpStackBefore { get; }
    ReadOnlySpan<Type> OpStackAfter { get; }
    IEnumerable<IInstructionInfo> Instructions { get; }
    IEnumerable<FlowEdge> Successors { get; }
}

internal interface ICilMethodSemanticModel
{
    int CodeByteSize { get; }
    int InstructionCount { get; }
    IInstructionInfo GetInstructionInfo(int index);
    int BasicBlockCount { get; }
    IBasicBlockInfo GetBasicBlockInfo(int index);
    IEnumerable<IBasicBlockInfo> BasicBlocks { get; }
    IEnumerable<FlowEdge> FlowEdges { get; }
}

sealed class CilMethodSemanticModel : ICilMethodSemanticModel
{
    public int CodeByteSize => throw new NotImplementedException();

    public int InstructionCount => throw new NotImplementedException();

    public int BasicBlockCount => throw new NotImplementedException();

    public IEnumerable<FlowEdge> FlowEdges => throw new NotImplementedException();

    static CilMethodSemanticModel Empty => throw new NotImplementedException();

    public static ICilMethodSemanticModel Create(MethodBase method)
    {
        var instructions = method.GetInstructions()?.ToImmutableArray() ?? [];
        if (instructions.Length == 0)
        {
            return Empty;
        }

        var model = new CilMethodSemanticModel();

        var nextOffsets = new int[instructions.Length];
        var offsetsToInstructionIndex = instructions.Index().ToFrozenDictionary((data) => data.Item.Offset, data => data.Index);

        {
            var methodBodyILByteSize = method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
            foreach (var (i, inst) in instructions.Index())
            {
                nextOffsets[i] = (i + 1) < instructions.Length ? instructions[i + 1].Offset : methodBodyILByteSize;
                var instSize = nextOffsets[i] - instructions[i].Offset;
                Debug.Assert(instSize > 0);
            }
        }

        var basicBlocks = new BasicBlock?[instructions.Length];
        var successors = new HashSet<IControlFlowEdge>?[instructions.Length];

        basicBlocks[0] = new BasicBlock(instructions[0].Offset);

        void AddEdge(int index, Func<BasicBlock, IControlFlowEdge> edgeFactory)
        {
            if (index < instructions.Length)
            {
                basicBlocks[index] ??= new BasicBlock(instructions[index].Offset)
                {
                    Index = index
                };
                successors[index] ??= [];
                successors[index]!.Add(edgeFactory(basicBlocks[index]!));
            }
        }

        void TryAddLead(int index)
        {
            if (index < instructions.Length)
            {
                basicBlocks[index] ??= new BasicBlock(instructions[index].Offset)
                {
                    Index = index
                };
            }
        }

        foreach (var (idx, inst) in instructions.Index())
        {
            successors[idx] ??= [];
            switch (inst.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    {
                        int jump = OpCodes.TakesSingleByteArgument(inst.OpCode) ? (sbyte)inst.Operand : (int)inst.Operand;
                        var instIndex = offsetsToInstructionIndex[nextOffsets[idx] + jump];
                        AddEdge(instIndex, static (bb) => new UnconditionalEdge(bb));
                        TryAddLead(idx + 1);
                        break;
                    }
                case FlowControl.Cond_Branch:
                    if (inst.OpCode.ToILOpCode() == System.Reflection.Metadata.ILOpCode.Switch)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        int jump = OpCodes.TakesSingleByteArgument(inst.OpCode) ? (sbyte)inst.Operand : (int)inst.Operand;
                        var instIndex = offsetsToInstructionIndex[nextOffsets[idx] + jump];
                        AddEdge(instIndex, static (bb) => new ConditionalEdge(bb));
                        AddEdge(idx + 1, static (bb) => new FallthroughEdge(bb));
                        break;
                    }
                case FlowControl.Return:
                    {
                        successors[idx]!.Add(new ReturnEdge());
                        TryAddLead(idx + 1);
                        break;
                    }
                case FlowControl.Throw:
                    TryAddLead(idx + 1);
                    throw new NotSupportedException("throw is not supported");
                // TODO: handle switch
                default:
                    continue;
            }
        }

        int bbCount = 0;
        var bbInstCount = new int[instructions.Length];
        BasicBlock? basicBlock = null;
        foreach (var (idx, bb) in basicBlocks.Index())
        {
            if (bb is not null)
            {
                basicBlock = bb;
                bbCount++;
            }
            bbInstCount[bbCount - 1]++;
            basicBlock.Successors = [.. basicBlock.Successors, .. successors[idx]];
        }

        foreach (var (idx, bb) in basicBlocks.Index())
        {
            if (bb is not null)
            {
                bb.Instructions = [.. instructions.Slice(idx, bbInstCount[bb.Index]).SelectMany(ParseInstruction)];
            }
        }

        throw new NotImplementedException();
    }

    public IBasicBlockInfo GetBasicBlockInfo(int index)
    {
        throw new NotImplementedException();
    }

    public IInstructionInfo GetInstructionInfo(int index)
    {
        throw new NotImplementedException();
    }
}
