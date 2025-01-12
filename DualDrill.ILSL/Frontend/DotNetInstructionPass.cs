using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed record class DotNetInstructionRepresentation(
    MethodBase Method,
    MethodBody Body,
    ImmutableArray<Instruction> Instructions,
    ImmutableArray<int> InstructionByteSizes,
    int CodeSize
) : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        writer.WriteLine($"{Instructions.Length} instructions ({CodeSize} bytes)");
        foreach (var (idx, inst) in Instructions.Index())
        {
            writer.WriteLine($"0x{inst.Offset:X8}({InstructionByteSizes[idx]:d4})\t{inst.OpCode} {inst.Operand}");
        }
    }
}
