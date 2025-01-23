using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace DualDrill.ILSL.Frontend;


interface IBasicBlock { }

interface IBasicBlock<TBasicBlock> : IBasicBlock
    where TBasicBlock : class, IBasicBlock<TBasicBlock>
{
    // successor of fallthrough, unconditional branch, default switch 
    void Dump(IndentedTextWriter writer);
}


sealed record class CILInstructionBasicBlock(ReadOnlyMemory<Instruction> Instructions)
{
}

public sealed record class DotNetInstructionBasicBlock(
    int BlockIndex,
    int LeadInstructionIndex,
    ImmutableArray<Instruction> Instructions
)
{
    public DotNetInstructionBasicBlock? Successor { get; set; } = null;
    public DotNetInstructionBasicBlock? ConditionalSuccessor { get; set; } = null;
    public int Offset => Instructions[0].Offset;
}


public sealed record class ControlFlowGraphDotNetInstructionRepresentation(
    DotNetInstructionBasicBlock EntryBlock,
    ImmutableArray<DotNetInstructionBasicBlock> BasicBlocks
) : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        writer.WriteLine($"{BasicBlocks.Length} blocks");

        writer.WriteLine($"entry block index = {EntryBlock.BlockIndex}");

        foreach (var b in BasicBlocks)
        {
            writer.WriteLine($"block #{b.BlockIndex} @{b.Offset:X8},{b.Instructions.Length} instructions, succ {b.Successor?.BlockIndex}, succ.if {b.ConditionalSuccessor?.BlockIndex} ");

            foreach (var inst in b.Instructions)
            {
                writer.WriteLine($"0x{inst.Offset:X8}\t{inst.OpCode} {inst.Operand}");
            }
        }
    }
}


public sealed record class ControlFlowGraphDotNetInstructionPass(
    CompilationContext Context,
    MethodBodyCompilation Compilation
)
    : IMethodBodyPass
{
    enum BranchKind
    {
        FallThrough,
        Unconditional,
        Conditional,
        Return
    }

    public ControlFlowGraphDotNetInstructionRepresentation Run(DotNetInstructionRepresentation data)
    {
        Debug.Assert(data.Instructions.Length > 0);
        var isLead = new bool[data.CodeSize];
        var target = new int[data.CodeSize];
        var kind = new BranchKind[data.CodeSize];
        isLead[0] = true;
        foreach (var (index, inst) in data.Instructions.Index())
        {
            var nextOffset = inst.Offset + data.InstructionByteSizes[index];
            var offset = inst.Offset;
            switch (inst.OpCode.ToILOpCode())
            {
                case ILOpCode.Br_s:
                    kind[offset] = BranchKind.Unconditional;
                    target[offset] = nextOffset + (sbyte)inst.Operand;
                    isLead[target[offset]] = true;
                    break;
                case ILOpCode.Brtrue_s:
                case ILOpCode.Brfalse_s:
                case ILOpCode.Bge_un_s:
                case ILOpCode.Beq_s:
                case ILOpCode.Bge_s:
                case ILOpCode.Bgt_s:
                case ILOpCode.Ble_s:
                case ILOpCode.Blt_s:
                case ILOpCode.Bne_un_s:
                    kind[offset] = BranchKind.Conditional;
                    target[offset] = nextOffset + (sbyte)inst.Operand;
                    isLead[target[offset]] = true;
                    break;
                case ILOpCode.Br:
                    kind[offset] = BranchKind.Unconditional;
                    target[offset] = nextOffset + (int)inst.Operand;
                    isLead[target[offset]] = true;
                    break;
                case ILOpCode.Brtrue:
                case ILOpCode.Brfalse:
                case ILOpCode.Bge_un:
                case ILOpCode.Beq:
                case ILOpCode.Bge:
                case ILOpCode.Bgt:
                case ILOpCode.Ble:
                case ILOpCode.Blt:
                case ILOpCode.Bne_un:
                    kind[offset] = BranchKind.Conditional;
                    target[offset] = nextOffset + (int)inst.Operand;
                    isLead[target[offset]] = true;
                    break;
                case ILOpCode.Switch:
                    {
                        throw new NotSupportedException();
                    }
                case ILOpCode.Ret:
                    kind[offset] = BranchKind.Return;
                    break;
                default:
                    continue;
            }
            if (nextOffset < isLead.Length)
            {
                isLead[nextOffset] = true;
            }
        }
        var basicBlockCount = 0;
        var offsetToBasicBlockIndex = new int[data.CodeSize];
        for (var i = 0; i < isLead.Length; i++)
        {
            if (isLead[i])
            {
                offsetToBasicBlockIndex[i] = basicBlockCount;
                basicBlockCount++;
            }
        }

        var result = new DotNetInstructionBasicBlock[basicBlockCount];

        var basicBlockLeadInstructionIndex = new int[basicBlockCount];
        var basicBlockInstructionCount = new int[basicBlockCount];

        var currentIndex = -1;
        foreach (var (index, inst) in data.Instructions.Index())
        {
            var offset = inst.Offset;
            if (isLead[offset])
            {
                currentIndex = offsetToBasicBlockIndex[offset];
                basicBlockLeadInstructionIndex[currentIndex] = index;
            }
            basicBlockInstructionCount[currentIndex]++;
        }

        for (var ib = 0; ib < basicBlockCount; ib++)
        {
            var index = basicBlockLeadInstructionIndex[ib];
            var offset = data.Instructions[index].Offset;
            var count = basicBlockInstructionCount[ib];
            result[ib] = new(ib, index, [.. data.Instructions[index..(index + count)]]);
        }


        foreach (var bb in result)
        {
            var exitInst = bb.Instructions[^1];
            var exitOffset = exitInst.Offset;
            switch (kind[exitOffset])
            {
                case BranchKind.FallThrough:
                    bb.Successor = result[bb.BlockIndex + 1];
                    break;
                case BranchKind.Unconditional:
                    bb.Successor = result[offsetToBasicBlockIndex[target[exitOffset]]];
                    break;
                case BranchKind.Conditional:
                    if (bb.BlockIndex + 1 < basicBlockCount)
                    {
                        bb.Successor = result[bb.BlockIndex + 1];
                    }
                    bb.ConditionalSuccessor = result[offsetToBasicBlockIndex[target[exitOffset]]];
                    break;
                case BranchKind.Return:
                    break;
                default:
                    throw new NotSupportedException($"Unknown branch kind {kind[exitOffset]}");
            }
        }
        return new(result[0], [.. result]);
    }

    public IFunctionBody Compile(IFunctionBody compilation)
    {
        throw new NotImplementedException();
    }
}
